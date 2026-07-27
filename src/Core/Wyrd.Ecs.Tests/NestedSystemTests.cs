namespace Wyrd.Ecs.Tests;

struct NestedPosition : IComponent { public float X; }
struct NestedVelocity : IComponent { public float X; }

sealed partial class MovementSystem : QuerySystem
{
    private static IQueryDefinition Build(World world) =>
        world.Query().With<Writes<NestedPosition>>().With<Reads<NestedVelocity>>();

    private partial void Execute(Time time, ref NestedPosition nestedPosition, in NestedVelocity nestedVelocity) => nestedPosition.X += nestedVelocity.X;
}

sealed partial class BoundsClampSystem : QuerySystem
{
    private static IQueryDefinition Build(World world) => world.Query().With<Writes<NestedPosition>>();

    private partial void Execute(Time time, ref NestedPosition nestedPosition)
    {
        if (nestedPosition.X > 10f) nestedPosition.X = 10f;
    }
}

public partial class NestedSystemTests
{
    [Fact]
    public void TwoDeclaredSystems_RunInSequence_EachSeesThePreviousOnesEffect()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new NestedPosition { X = 8f }, new NestedVelocity { X = 5f });
        world.ApplyCommands();

        var movement = new MovementSystem();
        var clamp = new BoundsClampSystem();

        world.RunOnce(movement, TimeSpan.Zero); // 8 + 5 = 13
        world.RunOnce(clamp, TimeSpan.Zero); // clamped to 10

        world.GetComponent<NestedPosition>(entity).X.Should().Be(10f);
    }

    [Fact]
    public void TwoDeclaredSystems_EachRegistersItsOwnGeneratedSystemAccessEntry()
    {
        Wyrd.Ecs.Generated.GeneratedSystemAccess.Entries.Should().ContainKey(typeof(MovementSystem));
        Wyrd.Ecs.Generated.GeneratedSystemAccess.Entries.Should().ContainKey(typeof(BoundsClampSystem));

        var movementAccess = Wyrd.Ecs.Generated.GeneratedSystemAccess.Entries[typeof(MovementSystem)];
        movementAccess.Writes.Should().Equal(typeof(NestedPosition));
        movementAccess.Reads.Should().Equal(typeof(NestedVelocity));
    }
}
