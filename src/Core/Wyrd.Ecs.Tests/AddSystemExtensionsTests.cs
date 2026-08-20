namespace Wyrd.Ecs.Tests;

struct SugarPosition : IComponent { public float X; }

sealed class SugarMarkerAnchor : MarkerSystem { }

sealed class SugarBeforeAnchorSystem : EcsSystem
{
    public static bool Ran;
    protected override void Execute(World world, Time time) => Ran = true;
}

[RunAfter(typeof(SugarMarkerAnchor))]
sealed class SugarAfterAnchorSystem : EcsSystem
{
    public static bool Ran;
    protected override void Execute(World world, Time time) => Ran = true;
}

sealed partial class SugarMoveSystem : QuerySystem
{
    protected override IQuery DefineQuery(Query query) => query.With<SugarPosition>();
    public void Update(Time time, ref SugarPosition sugarPosition) => sugarPosition.X += 1f;
}

sealed partial class SugarConstructedSystem : QuerySystem
{
    private readonly float _amount;
    public SugarConstructedSystem(float amount) => _amount = amount;

    protected override IQuery DefineQuery(Query query) => query.With<SugarPosition>();
    public void Update(Time time, ref SugarPosition sugarPosition) => sugarPosition.X += _amount;
}

public class AddSystemExtensionsTests
{
    [Fact]
    public void AddSystemGeneric_RegistersAParameterlessSystemWithoutTheGeneratedDictionary()
    {
        var world = new WorldBuilder().AddSystem<SugarMoveSystem>().Build();
        Entity entity = world.Commands.CreateEntity(new SugarPosition { X = 0f });
        world.ApplyCommands();

        world.Update(TimeSpan.Zero);

        world.GetComponent<SugarPosition>(entity).X.Should().Be(1f);
    }

    [Fact]
    public void AddSystemWithConfigure_RegistersAConstructorArgSystemWithoutTheGeneratedDictionary()
    {
        // SugarConstructedSystem's ctor(float) is neither ctor(World) nor parameterless,
        // so the generator emits no Construct entry for it - the Func<World, T> overload
        // is the only way to register it via AddSystem<T>().
        var world = new WorldBuilder().AddSystem<SugarConstructedSystem>(_ => new SugarConstructedSystem(5f)).Build();
        Entity entity = world.Commands.CreateEntity(new SugarPosition { X = 0f });
        world.ApplyCommands();

        world.Update(TimeSpan.Zero);

        world.GetComponent<SugarPosition>(entity).X.Should().Be(5f);
    }

    [Fact]
    public void AddSystemCore_RegistersAPreBuiltInstanceViaTheManualEscapeHatch()
    {
        // AddSystemCore is the non-generic, manual entry point AddSystem<T>() itself
        // closes over - exercised directly here for a type the generator never
        // discovered access for (an explicit access dictionary supplied by hand,
        // mirroring what a runtime-loaded system with no compile-time generator pass
        // would need).
        var access = new SystemAccess(Reads: [], Writes: [typeof(ScheduledPosition)]);
        var preBuilt = new MoveSystem();

        var world = new WorldBuilder()
            .AddSystemCore(typeof(MoveSystem), access, _ => preBuilt, [], [])
            .Build();
        Entity entity = world.Commands.CreateEntity();
        world.Commands.AddComponent(entity, new ScheduledPosition { X = 0f });
        world.ApplyCommands();

        world.Update(TimeSpan.Zero);

        world.GetComponent<ScheduledPosition>(entity).X.Should().Be(1f);
    }

    [Fact]
    public void AddSystem_ChainedBeforeAfter_ResolvesAMarkerAlongsideAGeneratedRunAfterEdge()
    {
        SugarBeforeAnchorSystem.Ran = false;
        SugarAfterAnchorSystem.Ran = false;

        // SugarAfterAnchorSystem's [RunAfter(typeof(SugarMarkerAnchor))] is seeded by the
        // generator automatically; SugarBeforeAnchorSystem's edge is declared fluently
        // instead, exercising both edge sources landing in the same graph.
        var world = new WorldBuilder()
            .AddSystem<SugarBeforeAnchorSystem>().Before<SugarMarkerAnchor>()
            .AddSystem<SugarAfterAnchorSystem>()
            .Build();

        world.Update(TimeSpan.Zero);

        SugarBeforeAnchorSystem.Ran.Should().BeTrue();
        SugarAfterAnchorSystem.Ran.Should().BeTrue();
    }
}
