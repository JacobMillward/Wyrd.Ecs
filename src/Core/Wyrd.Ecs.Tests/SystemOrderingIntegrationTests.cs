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

        // Physics and RenderPrep share no component access, so only the
        // RunAfter(EndOfPhysics) edge, never a data conflict, can force them into separate,
        // ordered stages. Network shares the anchor with neither and stays unconstrained.
        // IntegrationRenderPrepSystem's [RunAfter(typeof(EndOfPhysics))] is real, generator-seeded
        // data (Wyrd.Ecs.Generated.SystemRegistry.Edges), not a hand-built stand-in — this is the
        // one integration point in the suite proving that path end-to-end.
        Wyrd.Ecs.Generated.SystemRegistry.Edges.TryGetValue(typeof(IntegrationRenderPrepSystem), out var renderPrepEdges);

        var builder = new WorldBuilder();
        builder.AddSystemCore(
            typeof(IntegrationPhysicsSystem),
            new(Reads: [], Writes: [typeof(IntegrationPhysicsData)]),
            _ => new IntegrationPhysicsSystem(),
            generatedBeforeTargets: [typeof(EndOfPhysics)],
            generatedAfterTargets: []);
        builder.AddSystemCore(
            typeof(IntegrationRenderPrepSystem),
            new(Reads: [], Writes: [typeof(IntegrationRenderPrepData)]),
            _ => new IntegrationRenderPrepSystem(),
            generatedBeforeTargets: renderPrepEdges.Before ?? [],
            generatedAfterTargets: renderPrepEdges.After ?? []);
        builder.AddSystemCore(
            typeof(IntegrationNetworkSystem),
            new(Reads: [], Writes: []),
            _ => new IntegrationNetworkSystem(),
            generatedBeforeTargets: [],
            generatedAfterTargets: []);
        var world = builder.Build();

        world.Update(TimeSpan.Zero);

        IntegrationExecutionLog.Entries.Should().Contain(nameof(IntegrationPhysicsSystem));
        IntegrationExecutionLog.Entries.Should().Contain(nameof(IntegrationRenderPrepSystem));
        IntegrationExecutionLog.Entries.IndexOf(nameof(IntegrationPhysicsSystem))
            .Should().BeLessThan(IntegrationExecutionLog.Entries.IndexOf(nameof(IntegrationRenderPrepSystem)));
    }
}
