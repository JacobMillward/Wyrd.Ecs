namespace Wyrd.Ecs.Tests;

file struct Position : IComponent;
file struct Velocity : IComponent;
file struct Dead : ITag;
file struct BuffA : ITag;
file struct BuffB : ITag;

public class QueryChainTests
{
    [Fact]
    public void Query_ReturnsQueryOfNil()
    {
        var world = new World();

        var chain = world.Query();

        chain.Should().BeOfType<Query<Nil>>();
    }

    [Fact]
    public void With_PrependsTheComponentOntoTheShape()
    {
        var world = new World();

        var chain = world.Query().With<Position>();

        chain.Should().BeOfType<Query<(Position, Nil)>>();
    }

    [Fact]
    public void ChainedWith_NestsInCallOrder()
    {
        var world = new World();

        var chain = world.Query().With<Position>().With<Velocity>();

        chain.Should().BeOfType<Query<(Velocity, (Position, Nil))>>();
    }

    [Fact]
    public void Without_PrependsWithoutOntoTheShape()
    {
        var world = new World();

        var chain = world.Query().With<Position>().Without<Dead>();

        chain.Should().BeOfType<Query<(Without<Dead>, (Position, Nil))>>();
    }

    [Fact]
    public void Any_PrependsAnyOntoTheShape()
    {
        var world = new World();

        var chain = world.Query().With<Position>().Any<BuffA, BuffB>();

        chain.Should().BeOfType<Query<(Any<BuffA, BuffB>, (Position, Nil))>>();
    }
}
