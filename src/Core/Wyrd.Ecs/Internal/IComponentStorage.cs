namespace Wyrd.Ecs.Internal;

/// <summary>
/// Type-erased view over a <see cref="ComponentStorage{T}"/> used by <see cref="Archetype"/>
/// and <see cref="World"/> for operations that don't need to know the component type,
/// such as row bookkeeping and cross-archetype moves. <see cref="RawItems"/>/
/// <see cref="RawLastMarkedTick"/> are the full backing arrays (may be larger than the
/// archetype's live row count).
/// </summary>
internal interface IComponentStorage
{
    Array RawItems { get; }
    int[] RawLastMarkedTick { get; }

    void EnsureCapacity(int capacity);
    void SwapRemove(int row, int lastRow);

    /// <summary>
    /// Copies the value and tick-stamp at <paramref name="sourceRow"/> into
    /// <paramref name="destination"/> at <paramref name="destinationRow"/>. Carrying the
    /// tick-stamp across a structural move is required: without it, a component that
    /// legitimately changed just before the move would read as unchanged afterward.
    /// </summary>
    void CopyRowTo(int sourceRow, IComponentStorage destination, int destinationRow);

    /// <summary>Creates a fresh, empty storage of this same component type, already sized to <paramref name="capacity"/>.</summary>
    IComponentStorage CreateEmpty(int capacity);
}
