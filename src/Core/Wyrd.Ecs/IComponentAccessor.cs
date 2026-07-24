namespace Wyrd.Ecs;

/// <summary>
/// Self-referencing marker implemented by every chunk-level component accessor
/// (<see cref="Mut{T}"/>, <see cref="Ref{T}"/>). Used as the generic type argument
/// itself in <see cref="ChunkAction{TAccess0}"/> so a single query signature serves
/// every combination of tracked and read-only component access, without one overload
/// per combination.
/// </summary>
public interface IComponentAccessor<TSelf> where TSelf : struct, IComponentAccessor<TSelf>, allows ref struct
{
    /// <summary>The runtime <see cref="Internal.TypeIndex{T}"/> of the wrapped component type.</summary>
    static abstract int TypeIndex { get; }

    /// <summary>
    /// Constructs a chunk-level accessor over <paramref name="items"/>[<paramref name="start"/>,
    /// <paramref name="start"/>+<paramref name="length"/>). <paramref name="lastMarkedTick"/>
    /// is the parallel per-row last-marked-tick array. <paramref name="tracked"/> is true
    /// only when change tracking is currently on for this component type; <see cref="Ref{T}"/>
    /// implementations ignore <paramref name="tracked"/> and <paramref name="tick"/> alike,
    /// since they never mark anything dirty.
    /// </summary>
    static abstract TSelf CreateChunk(Array items, int[] lastMarkedTick, int tick, int start, int length, bool tracked);
}
