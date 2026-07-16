namespace Wyrd.Ecs;

/// <summary>
/// Self-referencing marker implemented by every chunk-level component accessor
/// (<see cref="Mut{T}"/>, <see cref="Ref{T}"/>). Used as the generic type argument
/// itself in <see cref="ChunkAction{TAccess0}"/> and <see cref="EntityQuery{TAccess0}"/>
/// so a single query signature serves every combination of tracked and read-only
/// component access, without one overload per combination. The static abstract members
/// let <see cref="World"/>'s query implementation recover the wrapped component type
/// and construct a chunk-level instance while remaining generic only over
/// <typeparamref name="TSelf"/> — the "exact generic plumbing" the design deferred to
/// this phase. <see cref="Array"/>/<c>bool[]</c> are used (not an internal storage type)
/// so this public interface's member signatures stay accessibility-consistent.
/// </summary>
public interface IComponentAccessor<TSelf> where TSelf : struct, IComponentAccessor<TSelf>, allows ref struct
{
    /// <summary>The runtime <see cref="Internal.TypeIndex{T}"/> of the wrapped component type.</summary>
    static abstract int TypeIndex { get; }

    /// <summary>
    /// Constructs a chunk-level accessor over <paramref name="items"/>[<paramref name="start"/>,
    /// <paramref name="start"/>+<paramref name="length"/>). <paramref name="dirty"/> is
    /// the parallel per-row dirty array — <see cref="Ref{T}"/> implementations ignore it.
    /// </summary>
    static abstract TSelf CreateChunk(Array items, bool[] dirty, int start, int length);
}
