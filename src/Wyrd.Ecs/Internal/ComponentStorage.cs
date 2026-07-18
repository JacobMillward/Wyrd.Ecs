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
    private T[] _items = new T[4];
    private int[] _lastMarkedTick = new int[4];
    private readonly DirtyLog _dirtyLog = new(Array.Empty<Entity>(), new DirtyEntry[4], 0);

    public Array RawItems => _items;
    public int[] RawLastMarkedTick => _lastMarkedTick;

    internal ref T this[int row] => ref _items[row];

    public void EnsureCapacity(int capacity)
    {
        GrowableArray.EnsureCapacity(ref _items, capacity);
        GrowableArray.EnsureCapacity(ref _lastMarkedTick, capacity);
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

    public void CopyRowTo(int sourceRow, IComponentStorage destination, int destinationRow) =>
        ((ComponentStorage<T>)destination)._items[destinationRow] = _items[sourceRow];

    public IComponentStorage CreateEmpty() => new ComponentStorage<T>();

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
        var entries = _dirtyLog.Entries.AsSpan(0, _dirtyLog.Count);
        var start = DirtyLogSearch.FindFirstAfter(entries, sinceTick);
        return entries[start..];
    }

    /// <summary>
    /// Drops every log entry with <c>Tick &lt;= tick</c> from the front of the log,
    /// shifting the remaining entries down. Called once per tick, only for component
    /// types with at least one live <see cref="ChangeConsumer{T}"/>, down to the
    /// minimum tick that consumer has advanced past.
    /// </summary>
    public void TrimBefore(int tick)
    {
        var entries = _dirtyLog.Entries.AsSpan(0, _dirtyLog.Count);
        var keepFrom = DirtyLogSearch.FindFirstAfter(entries, tick);
        if (keepFrom == 0) return;

        var remaining = _dirtyLog.Count - keepFrom;
        if (remaining > 0)
            Array.Copy(_dirtyLog.Entries, keepFrom, _dirtyLog.Entries, 0, remaining);
        _dirtyLog.Count = remaining;
    }

    private void EnsureDirtyLogCapacity(int additionalCapacity) =>
        GrowableArray.EnsureCapacity(ref _dirtyLog.Entries, _dirtyLog.Count + additionalCapacity);
}
