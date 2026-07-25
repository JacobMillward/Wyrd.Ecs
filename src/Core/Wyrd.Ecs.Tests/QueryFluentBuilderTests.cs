using System.Threading;

namespace Wyrd.Ecs.Tests;

file struct Position : IComponent { public float X; }
file struct Velocity : IComponent { public float X; }
file struct Dead : ITag;
file struct BuffA : ITag;
file struct BuffB : ITag;

public class QueryFluentBuilderTests
{
    private static World BuildWorld(out Entity alive, out Entity dead, out Entity buffed)
    {
        var world = new World();
        alive = world.Commands.CreateEntity();
        world.Commands.AddComponent(alive, new Position { X = 1f });
        world.Commands.AddComponent(alive, new Velocity { X = 1f });

        dead = world.Commands.CreateEntity();
        world.Commands.AddComponent(dead, new Position { X = 2f });
        world.Commands.AddComponent(dead, new Velocity { X = 1f });
        world.Commands.AddTag<Dead>(dead);

        buffed = world.Commands.CreateEntity();
        world.Commands.AddComponent(buffed, new Position { X = 3f });
        world.Commands.AddComponent(buffed, new Velocity { X = 1f });
        world.Commands.AddTag<BuffA>(buffed);

        world.ApplyCommands();
        return world;
    }

    [Fact]
    public void Without_ExcludesTaggedEntities()
    {
        var world = BuildWorld(out _, out _, out _);

        var count = 0;
        foreach (var row in world.Query<Position, Velocity>().Without<Dead>()) count++;

        count.Should().Be(2);
    }

    [Fact]
    public void Any_RequiresAtLeastOneOfTheGiven()
    {
        var world = BuildWorld(out _, out _, out var buffed);

        var matched = new List<Entity>();
        foreach (var row in world.Query<Position, Velocity>().Any<BuffA, BuffB>())
            matched.Add(row.Entity);

        matched.Should().ContainSingle().Which.Should().Be(buffed);
    }

    [Fact]
    public void Has_RequiresPresenceWithoutExposingAnAccessor()
    {
        var world = BuildWorld(out _, out _, out var buffed);

        var matched = new List<Entity>();
        foreach (var row in world.Query<Position>().Has<BuffA>())
            matched.Add(row.Entity);

        matched.Should().ContainSingle().Which.Should().Be(buffed);
    }

    [Fact]
    public void ForEach_VisitsEveryMatch_Sequentially()
    {
        var world = BuildWorld(out _, out _, out _);
        var total = 0f;

        world.Query<Position, Velocity>().ForEach(0f, (float _, ref Position p, ref Velocity v) => { total += p.X; });

        total.Should().Be(6f);
    }

    [Fact]
    public void ParallelForEach_VisitsEveryMatch()
    {
        var world = BuildWorld(out _, out _, out _);
        var total = 0;

        world.Query<Position, Velocity>().ParallelForEach(0, (int _, ref Position p, ref Velocity v) =>
            Interlocked.Increment(ref total));

        total.Should().Be(3);
    }

    [Fact]
    public void PlainQuery_WithNoFilterCalls_StillWorksUnchanged()
    {
        var world = BuildWorld(out _, out _, out _);

        var count = 0;
        foreach (var row in world.Query<Position, Velocity>()) count++;

        count.Should().Be(3);
    }
}
