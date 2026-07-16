namespace Wyrd.Ecs.Internal;

/// <summary>
/// Dense, growable, per-component-type storage backing one <see cref="Archetype"/>'s
/// column for <typeparamref name="T"/>: a struct-of-arrays column plus a parallel,
/// per-row dirty flag. Phase-4-minimal dirty tracking (a plain <c>bool</c> per row, no
/// tick/generation dedup) — see the archetype-storage plan's Global Constraints for why
/// that's deliberate and what the native-dirty-tracking phase replaces it with.
/// </summary>
internal sealed class ComponentStorage<T> : IComponentStorage where T : struct, IComponent
{
    private T[] _items = new T[4];
    private bool[] _dirty = new bool[4];

    public Array RawItems => _items;
    public bool[] RawDirty => _dirty;

    internal ref T this[int row] => ref _items[row];

    public void EnsureCapacity(int capacity)
    {
        if (_items.Length >= capacity) return;
        var newLength = Math.Max(capacity, _items.Length * 2);
        Array.Resize(ref _items, newLength);
        Array.Resize(ref _dirty, newLength);
    }

    public void SwapRemove(int row, int lastRow)
    {
        if (row != lastRow)
        {
            _items[row] = _items[lastRow];
            _dirty[row] = _dirty[lastRow];
        }
        _items[lastRow] = default;
        _dirty[lastRow] = false;
    }

    public void CopyRowTo(int sourceRow, IComponentStorage destination, int destinationRow) =>
        ((ComponentStorage<T>)destination)._items[destinationRow] = _items[sourceRow];

    public IComponentStorage CreateEmpty() => new ComponentStorage<T>();
}
