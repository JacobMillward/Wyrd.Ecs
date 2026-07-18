namespace Wyrd.Ecs.Internal;

/// <summary>
/// Dense, growable, per-component-type storage backing one <see cref="Archetype"/>'s
/// column for <typeparamref name="T"/>: a struct-of-arrays column plus a parallel,
/// per-row last-marked-tick generation counter, plus a growable, tick-stamped append
/// log of touched entities. See the design's Dirty-tracking section — the last-marked-
/// tick array is co-located with the dense array for cache locality, and the log is
/// the diff: an append-only, non-destructively-readable record of what changed.
/// </summary>
internal sealed class ComponentStorage<T> : IComponentStorage where T : struct, IComponent
{
    private T[] _items;
    private int[] _lastMarkedTick;
    private readonly DirtyLog _dirtyLog = new(Array.Empty<Entity>(), new DirtyEntry[4], 0);

    internal ComponentStorage(int capacity = 4)
    {
        _items = new T[capacity];
        _lastMarkedTick = new int[capacity];
    }

    public Array RawItems => _items;
    public int[] RawLastMarkedTick => _lastMarkedTick;

    internal ref T this[int row] => ref _items[row];

    public void EnsureCapacity(int capacity)
    {
        ArrayGrowth.EnsureCapacity(ref _items, capacity);
        ArrayGrowth.EnsureCapacity(ref _lastMarkedTick, capacity);
    }

    public void SwapRemove(int row, int lastRow)
    {
        if (row != lastRow)
        {
            _items[row] = _items[lastRow];
            _lastMarkedTick[row] = _lastMarkedTick[lastRow];
        }
        _items[lastRow] = default;
        _lastMarkedTick[lastRow] = 0;
    }

    public IComponentStorage CreateEmpty(int capacity) => new ComponentStorage<T>(capacity);

    /// <summary>
    /// Ensures the change log has room for at least <paramref name="additionalCapacity"/>
    /// more entries, refreshes its view of the archetype's current entity mapping, and
    /// returns it. Called once per archetype per chunk construction — see <see cref="DirtyLog"/>.
    /// </summary>
    public DirtyLog GetDirtyLogForChunk(Entity[] archetypeEntities, int additionalCapacity)
    {
        EnsureDirtyLogCapacity(additionalCapacity);
        _dirtyLog.ArchetypeEntities = archetypeEntities;
        return _dirtyLog;
    }

    /// <summary>
    /// Single-entity mark-dirty path used by <see cref="World.GetComponent{T}"/>/
    /// <see cref="World.AddComponent{T}"/> — the same tick-stamped dedup and log as the
    /// chunk-level <see cref="Mut{T}"/> accessor, for the one-entity convenience API.
    /// </summary>
    internal void MarkDirty(int row, Entity entity, int tick)
    {
        if (_lastMarkedTick[row] == tick) return;
        _lastMarkedTick[row] = tick;
        EnsureDirtyLogCapacity(1);
        _dirtyLog.Entries[_dirtyLog.Count] = new DirtyEntry(entity, tick);
        _dirtyLog.Count++;
    }

    /// <summary>Every log entry recorded after <paramref name="sinceTick"/>, in tick-ascending order.</summary>
    public ReadOnlySpan<DirtyEntry> ReadDirtyLogSince(int sinceTick)
    {
        var live = LiveEntries();
        var start = DirtyLogSearch.FindFirstAfter(live, sinceTick);
        return live[start..];
    }

    /// <summary>
    /// Marks every log entry with <c>Tick &lt;= tick</c> retired by advancing
    /// <see cref="DirtyLog.Head"/> past them — an O(log liveCount) binary search, no
    /// copy. Called once per tick, only for component types with at least one live
    /// <see cref="ChangeConsumer{T}"/>, down to the minimum tick that consumer has
    /// advanced past. The retired space isn't reclaimed here; see
    /// <see cref="EnsureDirtyLogCapacity"/>.
    /// </summary>
    public void TrimBefore(int tick)
    {
        var live = LiveEntries();
        if (live.Length == 0 || live[0].Tick > tick) return; // nothing new to retire

        _dirtyLog.Head += DirtyLogSearch.FindFirstAfter(live, tick);
    }

    private ReadOnlySpan<DirtyEntry> LiveEntries() =>
        _dirtyLog.Entries.AsSpan(_dirtyLog.Head, _dirtyLog.Count - _dirtyLog.Head);

    /// <summary>
    /// Ensures room for <paramref name="additionalCapacity"/> more appends at the tail.
    /// Unlike <see cref="ArrayGrowth.EnsureCapacity{T}"/>, this reclaims the space
    /// retired by <see cref="TrimBefore"/> first — shifting only the live entries down
    /// to index 0 — before growing the array, and folds that same shift into the copy
    /// a grow already has to do rather than paying for it separately. Compaction is
    /// therefore amortized into the append path, not something <see cref="TrimBefore"/>
    /// pays for every tick.
    /// </summary>
    private void EnsureDirtyLogCapacity(int additionalCapacity)
    {
        var required = _dirtyLog.Count + additionalCapacity;
        if (required <= _dirtyLog.Entries.Length) return;

        var live = _dirtyLog.Count - _dirtyLog.Head;
        if (live + additionalCapacity <= _dirtyLog.Entries.Length)
        {
            Array.Copy(_dirtyLog.Entries, _dirtyLog.Head, _dirtyLog.Entries, 0, live);
        }
        else
        {
            var newLength = Math.Max(live + additionalCapacity, Math.Max(_dirtyLog.Entries.Length * 2, 4));
            var newEntries = new DirtyEntry[newLength];
            Array.Copy(_dirtyLog.Entries, _dirtyLog.Head, newEntries, 0, live);
            _dirtyLog.Entries = newEntries;
        }

        _dirtyLog.Count = live;
        _dirtyLog.Head = 0;
    }
}
