using System.Threading;

namespace Wyrd.Ecs.Tests;

struct Position : IComponent { public float X; }
struct Velocity : IComponent { public float X; }
struct Dead : ITag;
struct BuffA : ITag;
struct BuffB : ITag;
struct Frozen : ITag;

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
        world.Query().With<Writes<Position>>().With<Reads<Velocity>>().Without<Dead>()
            .ForEach(0, (in int _, ref Position p, in Velocity v) => count++);

        count.Should().Be(2);
    }

    [Fact]
    public void Any_RequiresAtLeastOneOfTheGiven()
    {
        // BuildWorld's only BuffA/BuffB-tagged entity is "buffed", at Position.X = 3f.
        var world = BuildWorld(out _, out _, out _);

        var matchedPositions = new List<float>();
        world.Query().With<Reads<Position>>().Any<BuffA, BuffB>()
            .ForEach(matchedPositions, (in List<float> matches, in Position p) => matches.Add(p.X));

        matchedPositions.Should().Equal(3f);
    }

    [Fact]
    public void Has_RequiresPresenceWithoutExposingAnAccessor()
    {
        var world = BuildWorld(out _, out _, out var buffed);

        var matchedCount = 0;
        world.Query().With<Reads<Position>>().With<Has<BuffA>>()
            .ForEach(0, (in int _, in Position p) => matchedCount++);

        matchedCount.Should().Be(1);
    }

    [Fact]
    public void ForEach_VisitsEveryMatch_Sequentially()
    {
        var world = BuildWorld(out _, out _, out _);
        var total = 0f;

        world.Query().With<Writes<Position>>().With<Reads<Velocity>>()
            .ForEach(0f, (in float _, ref Position p, in Velocity v) => { total += p.X; });

        total.Should().Be(6f);
    }

    [Fact]
    public void ParallelForEach_VisitsEveryMatch()
    {
        var world = BuildWorld(out _, out _, out _);
        var total = 0;

        world.Query().With<Writes<Position>>().With<Reads<Velocity>>()
            .ParallelForEach(0, (in int _, ref Position p, in Velocity v) => Interlocked.Increment(ref total));

        total.Should().Be(3);
    }

    [Fact]
    public void PlainQuery_WithNoFilterCalls_StillWorksUnchanged()
    {
        var world = BuildWorld(out _, out _, out _);

        var count = 0;
        world.Query().With<Writes<Position>>().With<Reads<Velocity>>()
            .ForEach(0, (in int _, ref Position p, in Velocity v) => count++);

        count.Should().Be(3);
    }

    [Fact]
    public void PredicateForEach_ReturningFalse_StopsEarly()
    {
        var world = BuildWorld(out _, out _, out _);
        var visited = 0;

        world.Query().With<Reads<Position>>()
            .ForEach(0, (in int _, in Position p) =>
            {
                visited++;
                return visited < 2;
            });

        visited.Should().Be(2);
    }
}
