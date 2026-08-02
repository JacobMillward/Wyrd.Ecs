namespace Wyrd.Ecs.Internal;

/// <summary>
/// Dense, growable, per-component-type storage backing one <see cref="Archetype"/>'s
/// column for <typeparamref name="T"/>: a struct-of-arrays column plus a parallel
/// per-row last-marked-tick array. There is no separate change log; a reader scans this
/// array for rows whose tick is past its own watermark.
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

    /// <summary>Single-entity mark-dirty path: an unconditional stamp, no dedup needed since there's no log entry to duplicate.</summary>
    internal void MarkDirty(int row, int tick) => _lastMarkedTick[row] = tick;

    /// <summary>
    /// Writes <paramref name="value"/> to every row in <c>[startRow, startRow + count)</c>
    /// in one <see cref="Span{T}.Fill"/> call instead of <paramref name="count"/> individual
    /// writes. Caller must ensure capacity already covers the range.
    /// </summary>
    internal void Fill(int startRow, int count, T value) => _items.AsSpan(startRow, count).Fill(value);

    /// <summary>Bulk counterpart to <see cref="MarkDirty"/>: stamps every row in <c>[startRow, startRow + count)</c> with <paramref name="tick"/> in one <see cref="Span{T}.Fill"/> call.</summary>
    internal void MarkDirtyRange(int startRow, int count, int tick) => _lastMarkedTick.AsSpan(startRow, count).Fill(tick);
}
