namespace Wyrd.Ecs.Tests;

file sealed class FixedStepCountingSystem : EcsSystem
{
    public int ExecuteCount { get; private set; }
    protected override void Execute(World world, Time time) => ExecuteCount++;
}

public class WorldFixedTimestepTests
{
    [Fact]
    public void UnconfiguredFixedTimestep_DefaultsToOneSixtiethSecond()
    {
        var builder = new WorldBuilder();
        builder.AddSystemCore(typeof(FixedStepCountingSystem), access: null, _ => new FixedStepCountingSystem(), [], [], cadence: SystemCadence.Fixed);
        var world = builder.Build();

        world.Update(TimeSpan.FromSeconds(1.0 / 60.0));

        world.GetSystem<FixedStepCountingSystem>().ExecuteCount.Should().Be(1);
    }

    [Fact]
    public void ConfiguredFixedTimestep_StepsTheExpectedNumberOfTimes()
    {
        var builder = new WorldBuilder().WithFixedTimestep(TimeSpan.FromSeconds(0.1));
        builder.AddSystemCore(typeof(FixedStepCountingSystem), access: null, _ => new FixedStepCountingSystem(), [], [], cadence: SystemCadence.Fixed);
        var world = builder.Build();

        world.Update(TimeSpan.FromSeconds(0.35));

        world.GetSystem<FixedStepCountingSystem>().ExecuteCount.Should().Be(3); // 0.35 / 0.1 = 3 whole steps, 0.05s left over
        world.FixedStepAlpha.Should().BeApproximately(0.5, 0.0001); // 0.05 / 0.1
    }

    [Fact]
    public void OversizedDelta_ClampsToMaxSubstepsPerUpdate_AndDoesNotBacklogAcrossCalls()
    {
        var builder = new WorldBuilder().WithFixedTimestep(TimeSpan.FromSeconds(0.1), maxSubstepsPerUpdate: 3);
        builder.AddSystemCore(typeof(FixedStepCountingSystem), access: null, _ => new FixedStepCountingSystem(), [], [], cadence: SystemCadence.Fixed);
        var world = builder.Build();

        world.Update(TimeSpan.FromSeconds(10)); // wildly oversized: naive math would want 100 steps
        world.GetSystem<FixedStepCountingSystem>().ExecuteCount.Should().Be(3);

        // A second oversized call must not run MORE than 3 steps either — proves the backlog
        // was actually dropped (accumulator clamped), not merely deferred to catch up later.
        world.Update(TimeSpan.FromSeconds(10));
        world.GetSystem<FixedStepCountingSystem>().ExecuteCount.Should().Be(6);
    }

    [Fact]
    public void FixedStepAlpha_IsLiveEvenBeforeAnyFixedSystemIsRegistered()
    {
        var world = new World();

        world.Update(TimeSpan.FromSeconds(1.0 / 120.0));

        world.FixedStepAlpha.Should().BeApproximately(0.5, 0.0001); // half of the default 1/60s step, even with zero Fixed systems
    }

    [Fact]
    public void Pause_StopsTheFixedAccumulatorFromAdvancing()
    {
        var builder = new WorldBuilder().WithFixedTimestep(TimeSpan.FromSeconds(0.1));
        builder.AddSystemCore(typeof(FixedStepCountingSystem), access: null, _ => new FixedStepCountingSystem(), [], [], cadence: SystemCadence.Fixed);
        var world = builder.Build();
        world.Pause();

        world.Update(TimeSpan.FromSeconds(1));

        world.GetSystem<FixedStepCountingSystem>().ExecuteCount.Should().Be(0);
    }

    [Fact]
    public void TimeScale_MultipliesTheEffectiveDeltaFedToTheAccumulator()
    {
        var builder = new WorldBuilder().WithFixedTimestep(TimeSpan.FromSeconds(0.1));
        builder.AddSystemCore(typeof(FixedStepCountingSystem), access: null, _ => new FixedStepCountingSystem(), [], [], cadence: SystemCadence.Fixed);
        var world = builder.Build();
        world.TimeScale = 2.0;

        world.Update(TimeSpan.FromSeconds(0.1)); // 0.1 real * 2.0 scale = 0.2 virtual = 2 fixed steps

        world.GetSystem<FixedStepCountingSystem>().ExecuteCount.Should().Be(2);
    }
}
