namespace Wyrd.Ecs;

/// <summary>
/// A pure scheduling anchor: something an ordering edge can target so a new system can
/// be ordered relative to an established group (e.g. "after the whole physics phase")
/// without naming every member of that group. A concrete marker is a one-line
/// declaration (<c>public sealed class EndOfPhysics : MarkerSystem { }</c>) with
/// nothing to implement, since <see cref="MarkerSystem"/> declares no <c>Execute</c>.
/// It is never registered directly — it isn't an <see cref="EcsSystem"/>, so it can't
/// be passed to <c>WorldBuilder.WithSystems</c> — an instance is synthesized
/// automatically the first time an edge references its type, via its required public
/// parameterless constructor. It never appears in the schedule
/// <see cref="ScheduledExecutor"/> runs; it only ever shapes stage boundaries.
///
/// <para>
/// <b>Trimming/AOT caveat:</b> that synthesis happens via reflection
/// (<see cref="Activator.CreateInstance(Type)"/>), and no other code path ever
/// constructs a marker directly. A trimmed or Native AOT publish that never sees an
/// explicit reference to a marker type's constructor may legitimately strip it before
/// this ever runs, surfacing as a clear error at <c>WorldBuilder.Build()</c> rather
/// than silently — but if you use this feature under trimming/AOT, root each marker
/// type explicitly (e.g. a <c>[DynamicDependency]</c> naming its constructor, or any
/// reachable code that references it) so the constructor survives.
/// </para>
/// </summary>
public abstract class MarkerSystem : SchedulableSystem
{
}
