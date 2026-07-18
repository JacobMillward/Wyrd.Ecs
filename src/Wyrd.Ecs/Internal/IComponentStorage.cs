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

    /// <summary>
    /// Copies the value at <paramref name="sourceRow"/> into <paramref name="destination"/>
    /// at <paramref name="destinationRow"/>. A direct typed-array write once dispatched
    /// to the concrete <see cref="ComponentStorage{T}"/> — measured faster than routing
    /// the same copy through <see cref="Array.Copy(Array,int,Array,int,int)"/>'s
    /// <see cref="Array"/>-typed overload, which pays a runtime element-type
    /// compatibility check this call site doesn't need (both sides are already known by
    /// construction to hold the same component type).
    /// </summary>
    void CopyRowTo(int sourceRow, IComponentStorage destination, int destinationRow);

    /// <summary>Creates a fresh, empty storage of this same component type, already sized to <paramref name="capacity"/>.</summary>
    IComponentStorage CreateEmpty(int capacity);
    DirtyLog GetDirtyLogForChunk(Entity[] archetypeEntities, int additionalCapacity);
    ReadOnlySpan<DirtyEntry> ReadDirtyLogSince(int sinceTick);

    /// <summary>Removes every log entry with <c>Tick &lt;= tick</c>, keeping entries in order.</summary>
    void TrimBefore(int tick);
}
