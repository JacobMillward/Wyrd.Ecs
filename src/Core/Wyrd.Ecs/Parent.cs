namespace Wyrd.Ecs;

/// <summary>
/// Built-in scene-hierarchy relation: child edge points at its one parent.
/// <see cref="IExclusiveRelation"/> enforces at most one parent per entity: adding a
/// second replaces the first. <see cref="IDependent"/> makes destroying the parent
/// recursively destroy the whole subtree, not just unlink it. See <see cref="World"/>'s
/// parent/child accessors and <see cref="EntityView"/>'s parent/child mutators for the
/// dedicated API; the generic relation API works too.
/// </summary>
public readonly struct Parent : IRelation, IExclusiveRelation, IDependent
{
}
