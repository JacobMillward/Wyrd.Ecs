namespace Wyrd.Ecs.Tests;

file sealed class SchedulerFixedProbeSystem : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class SchedulerVariableProbeSystem : EcsSystem { protected override void Execute(World world, Time time) { } }
file sealed class SchedulerOtherVariableProbeSystem : EcsSystem { protected override void Execute(World world, Time time) { } }

public class ParallelSystemSchedulerCadenceTests
{
    [Fact]
    public void FixedAndVariableEntries_ArePlacedInIndependentStagePartitions()
    {
        var scheduler = new ParallelSystemScheduler(parallelThreshold: 1000);
        var world = new World(World.DefaultArchetypeCapacity, scheduler);

        var fixedEntry = new SystemEntry { SystemType = typeof(SchedulerFixedProbeSystem), Construct = w => new SchedulerFixedProbeSystem(), Cadence = SystemCadence.Fixed };
        var variableEntry = new SystemEntry { SystemType = typeof(SchedulerVariableProbeSystem), Construct = w => new SchedulerVariableProbeSystem(), Cadence = SystemCadence.Variable };
        scheduler.InitialRegister([fixedEntry, variableEntry], world);

        // A cross-cadence edge must throw once the schedule is (re)computed — proves the two
        // partitions are genuinely independent graphs, not one graph with a compatibility check.
        var crossCadenceEntry = new SystemEntry
        {
            SystemType = typeof(SchedulerOtherVariableProbeSystem),
            Construct = w => new SchedulerOtherVariableProbeSystem(),
            Cadence = SystemCadence.Variable,
            AfterTargets = [typeof(SchedulerFixedProbeSystem)],
        };
        scheduler.Register(crossCadenceEntry, world);

        var act = () => scheduler.Flush();

        act.Should().Throw<InvalidOperationException>().WithMessage("*SchedulerFixedProbeSystem*cadence*");
    }
}
