namespace Wyrd.Ecs;

/// <summary>
/// Marker interface implemented by every chunk-level component accessor
/// (<see cref="Mut{T}"/>, <see cref="Ref{T}"/>). Used as the generic type argument
/// itself in <see cref="ChunkAction{TAccess0}"/> and <see cref="EntityQuery{TAccess0}"/>
/// so a single query signature serves every combination of tracked and read-only
/// component access, without one overload per combination.
/// </summary>
public interface IComponentAccessor
{
}
