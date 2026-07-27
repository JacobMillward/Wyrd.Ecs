namespace Wyrd.Ecs.Tests;

struct SugarPosition : IComponent { public float X; }

sealed partial class SugarMoveSystem : QuerySystem
{
    private static IQueryDefinition Build(World world) => world.Query().With<Writes<SugarPosition>>();
    private partial void Execute(Time time, ref SugarPosition sugarPosition) => sugarPosition.X += 1f;
}

sealed partial class SugarConstructedSystem : QuerySystem
{
    private readonly float _amount;
    public SugarConstructedSystem(float amount) => _amount = amount;

    private static IQueryDefinition Build(World world) => world.Query().With<Writes<SugarPosition>>();
    private partial void Execute(Time time, ref SugarPosition sugarPosition) => sugarPosition.X += _amount;
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
}
