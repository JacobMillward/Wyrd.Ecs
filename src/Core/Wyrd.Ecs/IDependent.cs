namespace Wyrd.Ecs;

/// <summary>
/// Marker for a relation type whose destroy cascade destroys, rather than merely
/// unlinks. Implement alongside <see cref="IRelation"/> on any relation type meant to
/// behave this way: destroying the entity on the *target* side of an edge (the one
/// holding <see cref="RelationBacklinks{T}"/>) recursively destroys every entity on the
/// *source* side pointed at it, instead of the default behavior of just removing the
/// edge and leaving sources alive. See <see cref="RelationBacklinks{T}"/>'s own
/// <c>CascadeRemove</c> for where this is checked.
/// </summary>
public interface IDependent : IRelation
{
}
