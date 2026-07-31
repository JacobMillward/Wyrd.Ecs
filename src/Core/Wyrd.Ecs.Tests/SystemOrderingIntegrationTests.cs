namespace Wyrd.Ecs.Tests;

struct IntegrationPhysicsData : IComponent;
struct IntegrationRenderPrepData : IComponent;

sealed class EndOfPhysics : MarkerSystem { }

static class IntegrationExecutionLog
{
    public static readonly List<string> Entries = [];
}

sealed class IntegrationPhysicsSystem : EcsSystem
{
    protected override void Execute(World world, Time time) => IntegrationExecutionLog.Entries.Add(nameof(IntegrationPhysicsSystem));
}

[RunAfter(typeof(EndOfPhysics))]
sealed class IntegrationRenderPrepSystem : EcsSystem
{
    protected override void Execute(World world, Time time) => IntegrationExecutionLog.Entries.Add(nameof(IntegrationRenderPrepSystem));
}

sealed class IntegrationNetworkSystem : EcsSystem
{
    protected override void Execute(World world, Time time) => IntegrationExecutionLog.Entries.Add(nameof(IntegrationNetworkSystem));
}

public class SystemOrderingIntegrationTests
{
    [Fact]
    public void PhysicsRunsBeforeRenderPrepViaTheAnchorMarker_WithNoDataConflictBetweenThem()
    {
        IntegrationExecutionLog.Entries.Clear();

        // Disjoint component access -- Physics and RenderPrep never touch the same
        // component type, so the only thing that can force them into separate stages
        // (and therefore run in that order) is the RunAfter(typeof(EndOfPhysics)) edge
        // itself, never a data conflict. Network shares the anchor with neither and is
        // left fully unconstrained.
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(IntegrationPhysicsSystem)] = new(Reads: [], Writes: [typeof(IntegrationPhysicsData)]),
            [typeof(IntegrationRenderPrepSystem)] = new(Reads: [], Writes: [typeof(IntegrationRenderPrepData)]),
            [typeof(IntegrationNetworkSystem)] = new(Reads: [], Writes: []),
        };
        var physics = Order.For(new IntegrationPhysicsSystem()).Before<EndOfPhysics>();
        var renderPrep = new IntegrationRenderPrepSystem();
        var network = new IntegrationNetworkSystem();

        var world = new WorldBuilder().WithSystems(access, physics, renderPrep, network).Build();

        world.Tick(TimeSpan.Zero);

        IntegrationExecutionLog.Entries.Should().Contain(nameof(IntegrationPhysicsSystem));
        IntegrationExecutionLog.Entries.Should().Contain(nameof(IntegrationRenderPrepSystem));
        IntegrationExecutionLog.Entries.IndexOf(nameof(IntegrationPhysicsSystem))
            .Should().BeLessThan(IntegrationExecutionLog.Entries.IndexOf(nameof(IntegrationRenderPrepSystem)));
    }
}
