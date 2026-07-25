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
    public void With_PrependsTheMarkerOntoTheShape()
    {
        var world = new World();

        var chain = world.Query().With<Writes<Position>>();

        chain.Should().BeOfType<Query<(Writes<Position>, Nil)>>();
    }

    [Fact]
    public void ChainedWith_NestsInCallOrder()
    {
        var world = new World();

        var chain = world.Query().With<Writes<Position>>().With<Reads<Velocity>>();

        chain.Should().BeOfType<Query<(Reads<Velocity>, (Writes<Position>, Nil))>>();
    }

    [Fact]
    public void Without_PrependsWithoutOntoTheShape()
    {
        var world = new World();

        var chain = world.Query().With<Writes<Position>>().Without<Dead>();

        chain.Should().BeOfType<Query<(Without<Dead>, (Writes<Position>, Nil))>>();
    }

    [Fact]
    public void Any_PrependsAnyOntoTheShape()
    {
        var world = new World();

        var chain = world.Query().With<Writes<Position>>().Any<BuffA, BuffB>();

        chain.Should().BeOfType<Query<(Any<BuffA, BuffB>, (Writes<Position>, Nil))>>();
    }
}
