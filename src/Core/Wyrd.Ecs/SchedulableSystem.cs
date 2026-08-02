namespace Wyrd.Ecs;

/// <summary>
/// Common identity for anything that can be a node in the system-ordering graph: an
/// executable <see cref="EcsSystem"/>, or a non-executable <see cref="MarkerSystem"/>
/// anchor used only to express Before/After relationships. Carries no members of its
/// own; it exists purely so ordering edges (<see cref="RunBeforeAttribute"/>,
/// <see cref="RunAfterAttribute"/>, <see cref="OrderedSystem"/>) can target either kind
/// through one constraint.
/// </summary>
public abstract class SchedulableSystem
{
}
