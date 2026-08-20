namespace Wyrd.Ecs.Tests;

public class ComponentBundleTests
{
    public struct Position : IComponent { public float X; public float Y; }
    public struct Velocity : IComponent { public float X; public float Y; }

    public readonly record struct MovementBundle(float X, float Y) : IComponentBundle
    {
        public void ApplyTo<TSink>(TSink sink) where TSink : IComponentSink, allows ref struct =>
            new BundleBuilder<TSink>(sink)
                .Add(new Position { X = X, Y = Y })
                .Add(new Velocity { X = 1, Y = 0 });
    }

    [Fact]
    public void EntityView_Add_AppliesEveryComponentInTheBundle()
    {
        var world = new World();

        var entity = world.Commands.CreateEntity().Add(new MovementBundle(3, 4));
        world.ApplyCommands();

        world.GetComponent<Position>(entity.Entity).Should().Be(new Position { X = 3, Y = 4 });
        world.GetComponent<Velocity>(entity.Entity).Should().Be(new Velocity { X = 1, Y = 0 });
    }

    [Fact]
    public void EntityTemplate_Add_ProducesTheSameComponentsAsEntityView_Add()
    {
        var world = new World();
        var template = new EntityTemplate().Add(new MovementBundle(3, 4));

        var entity = world.Commands.CreateEntity(template);
        world.ApplyCommands();

        world.GetComponent<Position>(entity.Entity).Should().Be(new Position { X = 3, Y = 4 });
        world.GetComponent<Velocity>(entity.Entity).Should().Be(new Velocity { X = 1, Y = 0 });
    }
}
