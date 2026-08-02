namespace Wyrd.Ecs;

/// <summary>
/// Marker interface for a relation type that has at most one target at a time: the
/// "single parent" shape, as opposed to <see cref="IRelation"/>'s default many-targets
/// shape. <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/> checks for this
/// and, when present, removes any existing target before adding the new one, so adding
/// a second target replaces the first rather than accumulating alongside it. Implement
/// alongside <see cref="IRelation"/> on any relation type meant to behave this way, e.g.
/// a <c>Parent</c> relation.
/// </summary>
public interface IExclusiveRelation : IRelation
{
}
