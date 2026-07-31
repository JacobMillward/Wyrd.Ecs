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
    protected override IQuery DefineQuery(World world) => world.Query().With<SugarPosition>();
    public void Update(Time time, ref SugarPosition sugarPosition) => sugarPosition.X += 1f;
}

sealed partial class SugarConstructedSystem : QuerySystem
{
    private readonly float _amount;
    public SugarConstructedSystem(float amount) => _amount = amount;

    protected override IQuery DefineQuery(World world) => world.Query().With<SugarPosition>();
    public void Update(Time time, ref SugarPosition sugarPosition) => sugarPosition.X += _amount;
}

public class WithSystemsExtensionsTests
{
    [Fact]
    public void WithSystemsGeneric_RegistersAParameterlessSystemWithoutTheGeneratedDictionary()
    {
        var world = new WorldBuilder().WithSystems<SugarMoveSystem>().Build();
        var entity = world.Commands.CreateEntity(new SugarPosition { X = 0f });
        world.ApplyCommands();

        world.Tick(TimeSpan.Zero);

        world.GetComponent<SugarPosition>(entity).X.Should().Be(1f);
    }

    [Fact]
    public void WithSystemsInstances_RegistersAConstructorArgSystemWithoutTheGeneratedDictionary()
    {
        var world = new WorldBuilder().WithSystems(new SugarConstructedSystem(5f)).Build();
        var entity = world.Commands.CreateEntity(new SugarPosition { X = 0f });
        world.ApplyCommands();

        world.Tick(TimeSpan.Zero);

        world.GetComponent<SugarPosition>(entity).X.Should().Be(5f);
    }

    [Fact]
    public void WithSystemsInstances_RegistersAPreBuiltEcsSystemArrayVariable()
    {
        // The implicit EcsSystem -> OrderedSystem conversion only applies per
        // call-site argument, not across an entire array's element type, so a caller
        // that already assembled an EcsSystem[] (e.g. built programmatically) needs
        // the explicit IReadOnlyList<EcsSystem> overload, not the params one.
        EcsSystem[] preBuilt = [new SugarConstructedSystem(5f)];

        var world = new WorldBuilder().WithSystems(preBuilt).Build();
        var entity = world.Commands.CreateEntity(new SugarPosition { X = 0f });
        world.ApplyCommands();

        world.Tick(TimeSpan.Zero);

        world.GetComponent<SugarPosition>(entity).X.Should().Be(5f);
    }

    [Fact]
    public void WithSystemsDictionaryOverload_RegistersAPreBuiltEcsSystemArrayVariable()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(MoveSystem)] = new(Reads: [], Writes: [typeof(ScheduledPosition)]),
        };
        EcsSystem[] preBuilt = [new MoveSystem()];

        var world = new WorldBuilder().WithSystems(access, preBuilt).Build();
        var entity = world.Commands.CreateEntity();
        world.Commands.AddComponent(entity, new ScheduledPosition { X = 0f });
        world.ApplyCommands();

        world.Tick(TimeSpan.Zero);

        world.GetComponent<ScheduledPosition>(entity).X.Should().Be(1f);
    }

    [Fact]
    public void WithSystemsParamsOverload_ResolvesAMarkerWithNoExplicitDictionary()
    {
        SugarBeforeAnchorSystem.Ran = false;
        SugarAfterAnchorSystem.Ran = false;

        var world = new WorldBuilder()
            .WithSystems(Order.For(new SugarBeforeAnchorSystem()).Before<SugarMarkerAnchor>(), new SugarAfterAnchorSystem())
            .Build();

        world.Tick(TimeSpan.Zero);

        SugarBeforeAnchorSystem.Ran.Should().BeTrue();
        SugarAfterAnchorSystem.Ran.Should().BeTrue();
    }
}
