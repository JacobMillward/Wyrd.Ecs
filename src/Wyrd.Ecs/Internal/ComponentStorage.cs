namespace Wyrd.Ecs.Internal;

/// <summary>
/// Dense, growable, per-component-type storage backing one <see cref="Archetype"/>'s
/// column for <typeparamref name="T"/>: a struct-of-arrays column plus a parallel,
/// per-row last-marked-tick array. The last-marked-tick array is co-located with the
/// dense array for cache locality and is the entire change-tracking mechanism — there is
/// no separate log; a reader scans this array for rows whose tick is past its own
/// watermark.
/// </summary>
internal sealed class ComponentStorage<T> : IComponentStorage where T : struct, IComponent
{
    private T[] _items;
    private int[] _lastMarkedTick;

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

    public void CopyRowTo(int sourceRow, IComponentStorage destination, int destinationRow)
    {
        var typed = (ComponentStorage<T>)destination;
        typed._items[destinationRow] = _items[sourceRow];
        typed._lastMarkedTick[destinationRow] = _lastMarkedTick[sourceRow];
    }

    public IComponentStorage CreateEmpty(int capacity) => new ComponentStorage<T>(capacity);

    /// <summary>
    /// Single-entity mark-dirty path used by <see cref="World.GetComponent{T}"/>/
    /// <see cref="World.AddComponent{T}"/> — an unconditional stamp, no dedup, since
    /// there is no log entry to avoid duplicating.
    /// </summary>
    internal void MarkDirty(int row, int tick) => _lastMarkedTick[row] = tick;
}
