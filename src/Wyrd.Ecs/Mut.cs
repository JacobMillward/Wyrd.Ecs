namespace Wyrd.Ecs;

/// <summary>
/// Tracked, mutable chunk-level access to a <typeparamref name="T"/> component.
/// Indexing marks the specific entity dirty before returning a reference to its
/// component slot — this is the tracked mutation path; there is no separate untracked
/// accessor. Marking is tick-stamped and deduplicated: an entity touched many times in
/// one tick appends exactly one entry to the change log, not many. See the design's
/// Dirty-tracking section.
/// </summary>
public readonly ref struct Mut<T> : IComponentAccessor<Mut<T>> where T : struct, IComponent
{
    private readonly Span<T> _items;
    private readonly Span<int> _lastMarkedTick;
    private readonly int _tick;
    private readonly DirtyLog _dirtyLog;
    private readonly int _start;

    private Mut(Span<T> items, Span<int> lastMarkedTick, int tick, DirtyLog dirtyLog, int start)
    {
        _items = items;
        _lastMarkedTick = lastMarkedTick;
        _tick = tick;
        _dirtyLog = dirtyLog;
        _start = start;
    }

    /// <inheritdoc/>
    public static int TypeIndex => Internal.TypeIndex<T>.Value;

    /// <inheritdoc/>
    public static Mut<T> CreateChunk(Array items, int[] lastMarkedTick, int tick, DirtyLog dirtyLog, int start, int length) =>
        new(((T[])items).AsSpan(start, length), lastMarkedTick.AsSpan(start, length), tick, dirtyLog, start);

    /// <summary>The number of components accessible through this instance.</summary>
    public int Length => _items.Length;

    /// <summary>
    /// Marks the entity at <paramref name="index"/> dirty (appending to the change log
    /// only on its first touch this tick), then returns a mutable reference to its
    /// <typeparamref name="T"/> component.
    /// </summary>
    public ref T this[int index]
    {
        get
        {
            if (_lastMarkedTick[index] != _tick)
            {
                _lastMarkedTick[index] = _tick;
                _dirtyLog.Entries[_dirtyLog.Count] = new DirtyEntry(_dirtyLog.ArchetypeEntities[_start + index], _tick);
                _dirtyLog.Count++;
            }
            return ref _items[index];
        }
    }
}
