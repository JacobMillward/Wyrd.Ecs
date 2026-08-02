namespace Wyrd.Ecs;

/// <summary>
/// Implemented by every chunk-level component accessor (<see cref="Mut{T}"/>,
/// <see cref="Ref{T}"/>). Used as <see cref="ChunkAction{TAccess0}"/>'s generic
/// constraint.
/// </summary>
public interface IComponentAccessor<TSelf> where TSelf : struct, IComponentAccessor<TSelf>, allows ref struct
{
    /// <summary>The runtime <see cref="Internal.TypeIndex{T}"/> of the wrapped component type.</summary>
    static abstract int TypeIndex { get; }

    /// <summary>
    /// Constructs a chunk-level accessor over <paramref name="items"/>[<paramref name="start"/>,
    /// <paramref name="start"/>+<paramref name="length"/>). <paramref name="lastMarkedTick"/> is
    /// the parallel per-row last-marked-tick array. <paramref name="tracked"/> is true only when
    /// change tracking is on for this component type; <see cref="Ref{T}"/> ignores
    /// <paramref name="tracked"/> and <paramref name="tick"/>, since it never marks anything dirty.
    /// </summary>
    static abstract TSelf CreateChunk(Array items, int[] lastMarkedTick, int tick, int start, int length, bool tracked);
}
