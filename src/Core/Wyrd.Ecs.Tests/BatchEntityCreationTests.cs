namespace Wyrd.Ecs.Tests;

public class BatchEntityCreationTests
{
    [Fact]
    public void CreateEntity_Bare_ReturnsCountDistinctEntities()
    {
        var world = new World();

        var entities = world.Commands.CreateEntity(5);
        world.ApplyCommands();

        entities.Should().HaveCount(5);
        entities.Distinct().Should().HaveCount(5);
        entities.Should().OnlyContain(e => world.IsAlive(e));
    }

    [Fact]
    public void CreateEntity_Bare_ZeroCount_ReturnsEmptyArrayAndQueuesNothing()
    {
        var world = new World();

        var entities = world.Commands.CreateEntity(0);
        var act = () => world.ApplyCommands();

        entities.Should().BeEmpty();
        act.Should().NotThrow();
    }

    [Fact]
    public void CreateEntity_Bare_NegativeCount_ThrowsImmediately()
    {
        var world = new World();

        var act = () => world.Commands.CreateEntity(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateEntity_Bare_NotAliveUntilApplied()
    {
        var world = new World();

        var entities = world.Commands.CreateEntity(3);

        entities.Should().OnlyContain(e => !world.IsAlive(e));
    }
}
