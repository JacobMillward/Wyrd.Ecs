namespace Wyrd.Ecs;

/// <summary>
/// Marker for a relation type whose destroy cascade destroys rather than merely unlinks.
/// Implement alongside <see cref="IRelation"/>: destroying the entity on the target side
/// of an edge recursively destroys every source entity pointing at it, instead of just
/// removing the edge and leaving sources alive.
/// </summary>
public interface IDependent : IRelation
{
}
