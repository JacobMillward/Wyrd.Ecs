namespace Wyrd.Ecs;

/// <summary>
/// Implemented by a hand-written <see cref="EcsSystem"/> whose query shape isn't
/// expressed through the query-chain generator's <c>Reads&lt;T&gt;</c>/<c>Writes&lt;T&gt;</c>
/// markers (e.g. a runtime/config-driven filter) but that still wants to participate
/// in the static parallel schedule. <see cref="DescribeAccess"/> is called once, when
/// the schedule is built (<c>WorldBuilder.BuildWithExecutor</c>) — never per tick.
/// </summary>
public interface IQueryAccessDescriptor
{
    /// <summary>This system's component read/write footprint, for the scheduler's conflict graph.</summary>
    SystemAccess DescribeAccess();
}
