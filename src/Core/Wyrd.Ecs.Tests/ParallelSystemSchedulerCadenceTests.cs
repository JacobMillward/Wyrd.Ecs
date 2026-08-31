namespace Wyrd.Ecs.Tests;

file sealed class SchedulerFixedProbeSystem : EcsSystem
{
    public int ExecuteCount { get; private set; }
    protected override void Execute(World world, Time time) => ExecuteCount++;
}
file sealed class SchedulerVariableProbeSystem : EcsSystem
{
    public int ExecuteCount { get; private set; }
    protected override void Execute(World world, Time time) => ExecuteCount++;
}
file sealed class SchedulerOtherVariableProbeSystem : EcsSystem { protected override void Execute(World world, Time time) { } }

public class ParallelSystemSchedulerCadenceTests
{
    [Fact]
    public void FixedAndVariableEntries_ArePlacedInIndependentStagePartitions()
    {
        var scheduler = new ParallelSystemScheduler(parallelThreshold: 1000);
        var world = new World(World.DefaultArchetypeCapacity, scheduler, TimeSpan.FromSeconds(1.0 / 60.0), 5, TimeSpan.FromMilliseconds(250));

        var fixedEntry = new SystemEntry { SystemType = typeof(SchedulerFixedProbeSystem), Construct = w => new SchedulerFixedProbeSystem(), Cadence = SystemCadence.Fixed };
        var variableEntry = new SystemEntry { SystemType = typeof(SchedulerVariableProbeSystem), Construct = w => new SchedulerVariableProbeSystem(), Cadence = SystemCadence.Variable };
        scheduler.InitialRegister([fixedEntry, variableEntry], world);

        // A cross-cadence edge must throw once the schedule is (re)computed: proves the two
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

    [Fact]
    public void RunStages_WithFixedCadence_OnlyInvokesFixedCadenceSystems()
    {
        var scheduler = new ParallelSystemScheduler(parallelThreshold: 1000);
        var world = new World(World.DefaultArchetypeCapacity, scheduler, TimeSpan.FromSeconds(1.0 / 60.0), 5, TimeSpan.FromMilliseconds(250));

        var fixedEntry = new SystemEntry { SystemType = typeof(SchedulerFixedProbeSystem), Construct = w => new SchedulerFixedProbeSystem(), Cadence = SystemCadence.Fixed };
        var variableEntry = new SystemEntry { SystemType = typeof(SchedulerVariableProbeSystem), Construct = w => new SchedulerVariableProbeSystem(), Cadence = SystemCadence.Variable };
        scheduler.InitialRegister([fixedEntry, variableEntry], world);

        scheduler.RunStages(world, new Time(TimeSpan.Zero, TimeSpan.Zero), SystemCadence.Fixed);

        ((SchedulerFixedProbeSystem)fixedEntry.Instance!).ExecuteCount.Should().Be(1);
        ((SchedulerVariableProbeSystem)variableEntry.Instance!).ExecuteCount.Should().Be(0);

        scheduler.RunStages(world, new Time(TimeSpan.Zero, TimeSpan.Zero), SystemCadence.Variable);

        ((SchedulerFixedProbeSystem)fixedEntry.Instance!).ExecuteCount.Should().Be(1);
        ((SchedulerVariableProbeSystem)variableEntry.Instance!).ExecuteCount.Should().Be(1);
    }
}
