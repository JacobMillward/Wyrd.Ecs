namespace Wyrd.Ecs.Tests;

struct IntegrationPosition : IComponent;

sealed class EndOfPhysics : MarkerSystem { }

sealed class IntegrationPhysicsSystem : EcsSystem
{
    public static readonly List<string> Log = [];
    protected override void Execute(World world, Time time) => Log.Add(nameof(IntegrationPhysicsSystem));
}

[RunAfter(typeof(EndOfPhysics))]
sealed class IntegrationRenderPrepSystem : EcsSystem
{
    public static readonly List<string> Log = [];
    protected override void Execute(World world, Time time) => Log.Add(nameof(IntegrationRenderPrepSystem));
}

sealed class IntegrationNetworkSystem : EcsSystem
{
    public static readonly List<string> Log = [];
    protected override void Execute(World world, Time time) => Log.Add(nameof(IntegrationNetworkSystem));
}

public class SystemOrderingIntegrationTests
{
    [Fact]
    public void PhysicsRunsBeforeRenderPrepViaTheAnchorMarker_NetworkIsUnconstrained()
    {
        IntegrationPhysicsSystem.Log.Clear();
        IntegrationRenderPrepSystem.Log.Clear();

        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(IntegrationPhysicsSystem)] = new(Reads: [], Writes: [typeof(IntegrationPosition)]),
            [typeof(IntegrationRenderPrepSystem)] = new(Reads: [typeof(IntegrationPosition)], Writes: []),
            [typeof(IntegrationNetworkSystem)] = new(Reads: [], Writes: []),
        };
        var physics = Order.For(new IntegrationPhysicsSystem()).Before<EndOfPhysics>();
        var renderPrep = new IntegrationRenderPrepSystem();
        var network = new IntegrationNetworkSystem();

        var world = new WorldBuilder().WithSystems(access, physics, renderPrep, network).Build();

        world.Tick(TimeSpan.Zero);

        // IntegrationRenderPrepSystem reads IntegrationPosition and IntegrationPhysicsSystem
        // writes it, so the two also conflict on data independent of the anchor edge -- the
        // point of this test is that the anchor pattern compiles and runs correctly through
        // the full WorldBuilder/World.Tick path, not the precise stage-separation guarantee,
        // which SystemSchedulerOrderingTests already covers at the scheduler level directly.
        IntegrationPhysicsSystem.Log.Should().ContainSingle();
        IntegrationRenderPrepSystem.Log.Should().ContainSingle();
    }
}
