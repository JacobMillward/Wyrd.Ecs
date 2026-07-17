namespace Wyrd.Ecs.Internal;

/// <summary>
/// Type-erased view over a <see cref="ComponentStorage{T}"/> used by <see cref="Archetype"/>
/// and <see cref="World"/> for operations that don't need to know the component type —
/// row bookkeeping, cross-archetype moves, and dirty-log access. <see cref="RawItems"/>/
/// <see cref="RawLastMarkedTick"/> are the full backing arrays (may be larger than the
/// archetype's live row count).
/// </summary>
internal interface IComponentStorage
{
    Array RawItems { get; }
    int[] RawLastMarkedTick { get; }

    void EnsureCapacity(int capacity);
    void SwapRemove(int row, int lastRow);
    void CopyRowTo(int sourceRow, IComponentStorage destination, int destinationRow);
    IComponentStorage CreateEmpty();
    DirtyLog GetDirtyLogForChunk(Entity[] archetypeEntities, int additionalCapacity);
    ReadOnlySpan<DirtyEntry> ReadDirtyLogSince(int sinceTick);

    /// <summary>Removes every log entry with <c>Tick &lt;= tick</c>, keeping entries in order.</summary>
    void TrimBefore(int tick);
}
