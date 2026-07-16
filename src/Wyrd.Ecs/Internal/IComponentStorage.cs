namespace Wyrd.Ecs.Internal;

/// <summary>
/// Type-erased view over a <see cref="ComponentStorage{T}"/> used by <see cref="Archetype"/>
/// and <see cref="World"/> for operations that don't need to know the component type —
/// row bookkeeping and cross-archetype moves. <see cref="RawItems"/>/<see cref="RawDirty"/>
/// are the full backing arrays (may be larger than the archetype's live row count).
/// </summary>
internal interface IComponentStorage
{
    Array RawItems { get; }
    bool[] RawDirty { get; }

    void EnsureCapacity(int capacity);
    void SwapRemove(int row, int lastRow);
    void CopyRowTo(int sourceRow, IComponentStorage destination, int destinationRow);
    IComponentStorage CreateEmpty();
}
