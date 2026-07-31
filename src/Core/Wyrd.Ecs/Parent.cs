namespace Wyrd.Ecs;

/// <summary>
/// Built-in scene-hierarchy relation: child edge points at its one parent.
/// <see cref="IExclusiveRelation"/> enforces at most one parent per entity — adding a
/// second replaces the first. <see cref="IDependent"/> makes destroying the parent
/// recursively destroy the whole subtree, not just unlink it. See <see cref="World"/>'s
/// <c>TryGetParent</c>/<c>GetParent</c>/<c>Children</c>/<c>Ancestors</c>/<c>Descendants</c>
/// for the dedicated read API, and <see cref="EntityView"/>'s
/// <c>SetParent</c>/<c>ClearParent</c>/<c>AddChild</c>/<c>RemoveChild</c> for the
/// dedicated mutation API. The generic relation API (<c>AddRelation&lt;Parent&gt;</c>,
/// <c>Targets&lt;Parent&gt;</c>, <c>Sources&lt;Parent&gt;</c>, etc.) works too.
/// </summary>
public readonly struct Parent : IRelation, IExclusiveRelation, IDependent
{
}
