namespace Wyrd.Ecs.Tests;

public class WorldTagTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private struct Frozen : ITag;

    [Fact]
    public void AddTag_EntityThenHasIt()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddTag<Frozen>(entity);
        world.ApplyCommands();

        world.HasTag<Frozen>(entity).Should().BeTrue();
    }

    [Fact]
    public void AddTag_Twice_IsANoOp()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.Commands.AddTag<Frozen>(entity);
        world.ApplyCommands();

        world.Commands.AddTag<Frozen>(entity);
        var act = () => world.ApplyCommands();

        act.Should().NotThrow();
        world.HasTag<Frozen>(entity).Should().BeTrue();
    }

    [Fact]
    public void RemoveTag_Present_RemovesIt()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.Commands.AddTag<Frozen>(entity);
        world.ApplyCommands();

        world.Commands.RemoveTag<Frozen>(entity);
        world.ApplyCommands();

        world.HasTag<Frozen>(entity).Should().BeFalse();
    }

    [Fact]
    public void RemoveTag_Missing_IsANoOp()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.RemoveTag<Frozen>(entity);
        var act = () => world.ApplyCommands();

        act.Should().NotThrow();
    }

    [Fact]
    public void HasTag_Missing_ReturnsFalse()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.HasTag<Frozen>(entity).Should().BeFalse();
    }

    [Fact]
    public void AddTag_PreservesExistingComponentValues()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position { X = 42f });
        world.ApplyCommands();

        world.Commands.AddTag<Frozen>(entity);
        world.ApplyCommands();

        world.GetComponent<Position>(entity).X.Should().Be(42f);
        world.HasTag<Frozen>(entity).Should().BeTrue();
    }

    [Fact]
    public void RemoveTag_PreservesExistingComponentValues()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position { X = 42f });
        world.Commands.AddTag<Frozen>(entity);
        world.ApplyCommands();

        world.Commands.RemoveTag<Frozen>(entity);
        world.ApplyCommands();

        world.GetComponent<Position>(entity).X.Should().Be(42f);
    }
}
