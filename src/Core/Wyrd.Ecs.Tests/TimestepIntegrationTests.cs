namespace Wyrd.Ecs.Tests;

sealed class TimestepIntegrationPhysicsSystem : EcsSystem
{
    public int ExecuteCount { get; private set; }
    protected override void Execute(World world, Time time) => ExecuteCount++;
}

[FixedTimestep]
[RunAfter(typeof(TimestepIntegrationPhysicsSystem))] // cross-cadence: TimestepIntegrationPhysicsSystem is Variable, this is Fixed
sealed class TimestepIntegrationBadCrossCadenceSystem : EcsSystem
{
    protected override void Execute(World world, Time time) { }
}

[FixedTimestep]
sealed class TimestepIntegrationFixedPhysicsSystem : EcsSystem
{
    public int ExecuteCount { get; private set; }
    protected override void Execute(World world, Time time) => ExecuteCount++;
}

[RunAfter(typeof(TimestepIntegrationFixedPhysicsSystem))] // Variable ordered after a Fixed system: also cross-cadence (the "lying" direction), must also throw
sealed class TimestepIntegrationCameraFollowSystem : EcsSystem
{
    protected override void Execute(World world, Time time) { }
}

public class TimestepIntegrationTests
{
    [Fact]
    public void CrossCadenceOrderingEdge_ThrowsAtBuild()
    {
        // InitialRegister (called from Build()) recomputes both partitions eagerly, so a
        // cross-cadence edge throws here, not on the first Update() call.
        var act = () => new WorldBuilder().AddSystem<TimestepIntegrationPhysicsSystem>().AddSystem<TimestepIntegrationBadCrossCadenceSystem>().Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*cadence*");
    }

    [Fact]
    public void VariableOrderedAfterFixed_AlsoThrows_NotJustTheReverseDirection()
    {
        var act = () => new WorldBuilder().AddSystem<TimestepIntegrationFixedPhysicsSystem>().AddSystem<TimestepIntegrationCameraFollowSystem>().Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*cadence*");
    }

    [Fact]
    public void FixedCadenceSystem_RunsUnderTheDefaultBuilderWithNoOrderingEdgesAtAll()
    {
        var builder = new WorldBuilder().WithFixedTimestep(TimeSpan.FromSeconds(1)).AddSystem<TimestepIntegrationFixedPhysicsSystem>();
        var world = builder.Build();

        world.Update(TimeSpan.FromSeconds(1));

        world.GetSystem<TimestepIntegrationFixedPhysicsSystem>().ExecuteCount.Should().Be(1);
    }

    [Fact]
    public void PausedWorld_StopsFixedSteppingButVariablePassStillRuns()
    {
        var builder = new WorldBuilder().WithFixedTimestep(TimeSpan.FromSeconds(0.1))
            .AddSystem<TimestepIntegrationFixedPhysicsSystem>()
            .AddSystem<TimestepIntegrationPhysicsSystem>();
        var world = builder.Build();
        world.Pause();

        world.Update(TimeSpan.FromSeconds(1));

        world.GetSystem<TimestepIntegrationFixedPhysicsSystem>().ExecuteCount.Should().Be(0, "the fixed accumulator must not advance while paused");
        world.GetSystem<TimestepIntegrationPhysicsSystem>().ExecuteCount.Should().Be(1, "the Variable pass still runs once per Update even while paused, just with Delta == Zero");
    }
}
