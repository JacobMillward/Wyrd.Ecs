namespace Wyrd.Ecs;

/// <summary>
/// A pure scheduling anchor: something an ordering edge can target so a new system can
/// be ordered relative to an established group (e.g. "after the whole physics phase")
/// without naming every member of that group. A concrete marker is a one-line
/// declaration (<c>public sealed class EndOfPhysics : MarkerSystem { }</c>) with
/// nothing to implement, since <see cref="MarkerSystem"/> declares no <c>Execute</c>.
/// It is never registered — it isn't an <see cref="EcsSystem"/>, so it can't be passed
/// to <c>WorldBuilder.WithSystems</c> — and, unlike a system, it is never
/// <em>instantiated</em> either: the ordering graph tracks a marker purely by its
/// <see cref="Type"/> (see <c>Internal.OrderNode</c>), since a marker has no state or
/// behavior for an instance to hold in the first place. No constructor of any kind is
/// required. This is also what makes referencing a marker declared in a different
/// assembly (e.g. an addon package's own extension point) work with zero extra
/// machinery — a <see cref="Type"/> already resolves across assembly boundaries on its
/// own; there is nothing to construct, generate, or reflect over. A marker never
/// appears in the schedule <see cref="ScheduledExecutor"/> runs; it only ever shapes
/// stage boundaries.
/// </summary>
public abstract class MarkerSystem : SchedulableSystem
{
}
