namespace Wyrd.Ecs;

/// <summary>
/// Self-referencing marker implemented by every chunk-level component accessor
/// (<see cref="Mut{T}"/>, <see cref="Ref{T}"/>). Used as the generic type argument
/// itself in <see cref="ChunkAction{TAccess0}"/> so a single query signature serves
/// every combination of tracked and read-only component access, without one overload
/// per combination. The static abstract members let <see cref="World"/>'s query
/// implementation recover the wrapped component type and construct a chunk-level
/// instance while remaining generic only over <typeparamref name="TSelf"/>.
/// <see cref="Array"/>/<see cref="int"/>[]/<see cref="DirtyLog"/> are used (not an
/// internal storage type) so this public interface's member signatures stay
/// accessibility-consistent.
/// </summary>
public interface IComponentAccessor<TSelf> where TSelf : struct, IComponentAccessor<TSelf>, allows ref struct
{
    /// <summary>The runtime <see cref="Internal.TypeIndex{T}"/> of the wrapped component type.</summary>
    static abstract int TypeIndex { get; }

    /// <summary>
    /// Constructs a chunk-level accessor over <paramref name="items"/>[<paramref name="start"/>,
    /// <paramref name="start"/>+<paramref name="length"/>). <paramref name="lastMarkedTick"/>
    /// is the parallel per-row last-marked-tick array and <paramref name="dirtyLog"/> is
    /// the growable change log for this archetype/component type. <paramref name="tracked"/>
    /// is true only when at least one consumer is currently registered for this component
    /// type; <see cref="Ref{T}"/> implementations ignore <paramref name="tracked"/>,
    /// <paramref name="tick"/>, and <paramref name="dirtyLog"/> alike, since they never
    /// mark anything dirty. See the design's Dirty-tracking section.
    /// </summary>
    static abstract TSelf CreateChunk(Array items, int[] lastMarkedTick, int tick, DirtyLog dirtyLog, int start, int length, bool tracked);
}
