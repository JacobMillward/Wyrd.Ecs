namespace Wyrd.Ecs;

/// <summary>
/// A pure scheduling anchor: something an ordering edge can target so a new system can
/// be ordered relative to an established group (e.g. "after the whole physics phase")
/// without naming every member of that group. A concrete marker is a one-line
/// declaration (<c>public sealed class EndOfPhysics : MarkerSystem { }</c>). It is never
/// registered or instantiated: the ordering graph tracks a marker purely by its
/// <see cref="Type"/>, and it never appears in the schedule
/// <see cref="ScheduledExecutor"/> runs; it only shapes stage boundaries.
/// </summary>
public abstract class MarkerSystem : SchedulableSystem
{
}
