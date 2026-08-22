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

    /// <summary>
    /// Closes the gap left by the row's departure: backfills <paramref name="row"/> from
    /// <paramref name="lastRow"/> when they differ, and resets the vacated tail to default
    /// so make-live paths never observe a prior occupant's bytes.
    /// </summary>
    void CloseGap(int row, int lastRow);

    /// <summary>One-pass structural move out of this archetype: copies the row into <paramref name="destination"/>, carrying its tick stamp, then closes the gap left by the move.</summary>
    void MoveRowAndCloseGap(int sourceRow, int sourceLastRow, IComponentStorage destination, int destinationRow);

    /// <summary>Creates a fresh, empty storage of this same component type, already sized to <paramref name="capacity"/>.</summary>
    IComponentStorage CreateEmpty(int capacity);
}
