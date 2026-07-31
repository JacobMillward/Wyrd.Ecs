namespace Wyrd.Ecs.Tests;

public class EntityViewTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    [Fact]
    public void Entity_ReturnsTheBoundEntity()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].Entity.Should().Be(entity);
    }

    [Fact]
    public void GetComponent_ReturnsATrackedReferenceToTheValue()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();

        world[entity].GetComponent<Position>().X.Should().Be(5f);
    }

    [Fact]
    public void TryGetComponent_Missing_ReturnsNotFound()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].TryGetComponent<Position>(out var found);

        found.Should().BeFalse();
    }

    [Fact]
    public void TryGetComponent_Present_ReturnsFoundAndTheTrackedValue()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();

        ref var value = ref world[entity].TryGetComponent<Position>(out var found);

        found.Should().BeTrue();
        value.X.Should().Be(5f);
    }

    [Fact]
    public void HasComponent_Present_ReturnsTrue()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();

        world[entity].HasComponent<Position>().Should().BeTrue();
    }

    [Fact]
    public void HasComponent_Missing_ReturnsFalse()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].HasComponent<Position>().Should().BeFalse();
    }

    [Fact]
    public void AddComponent_QueuesTheAdd_VisibleAfterApplyCommands()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world[entity].AddComponent(new Position { X = 3f });
        world.HasComponent<Position>(entity).Should().BeFalse(); // still deferred
        world.ApplyCommands();

        world.GetComponent<Position>(entity).X.Should().Be(3f);
    }

    [Fact]
    public void RemoveComponent_QueuesTheRemove_VisibleAfterApplyCommands()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();

        world[entity].RemoveComponent<Position>();
        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Fact]
    public void AddComponent_ReturnsTheSameViewForChaining()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        var view = world[entity].AddComponent(new Position { X = 1f });

        view.Entity.Should().Be(entity);
    }
}
