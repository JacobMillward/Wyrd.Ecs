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
        var entity = world.CreateEntity();

        world.AddTag<Frozen>(entity);

        world.HasTag<Frozen>(entity).Should().BeTrue();
    }

    [Fact]
    public void AddTag_Twice_IsANoOp()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddTag<Frozen>(entity);

        var act = () => world.AddTag<Frozen>(entity);

        act.Should().NotThrow();
        world.HasTag<Frozen>(entity).Should().BeTrue();
    }

    [Fact]
    public void RemoveTag_Present_RemovesIt()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddTag<Frozen>(entity);

        world.RemoveTag<Frozen>(entity);

        world.HasTag<Frozen>(entity).Should().BeFalse();
    }

    [Fact]
    public void RemoveTag_Missing_IsANoOp()
    {
        var world = new World();
        var entity = world.CreateEntity();

        var act = () => world.RemoveTag<Frozen>(entity);

        act.Should().NotThrow();
    }

    [Fact]
    public void HasTag_Missing_ReturnsFalse()
    {
        var world = new World();
        var entity = world.CreateEntity();

        world.HasTag<Frozen>(entity).Should().BeFalse();
    }

    [Fact]
    public void AddTag_PreservesExistingComponentValues()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 42f;

        world.AddTag<Frozen>(entity);

        world.GetComponent<Position>(entity).X.Should().Be(42f);
        world.HasTag<Frozen>(entity).Should().BeTrue();
    }

    [Fact]
    public void RemoveTag_PreservesExistingComponentValues()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 42f;
        world.AddTag<Frozen>(entity);

        world.RemoveTag<Frozen>(entity);

        world.GetComponent<Position>(entity).X.Should().Be(42f);
    }
}
