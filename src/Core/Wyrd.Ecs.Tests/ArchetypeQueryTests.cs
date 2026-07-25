namespace Wyrd.Ecs.Tests;

file struct Position : IComponent { public float X; }
file struct Velocity : IComponent { public float X; }
file struct Dead : ITag;
file struct BuffA : ITag;
file struct BuffB : ITag;

public class ArchetypeQueryTests
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
    public void Resolve_YieldsOneChunkPerMatchingArchetype()
    {
        var world = BuildWorld(out _, out _, out _);

        var query = ArchetypeQuery.Empty.Has<Position>().Has<Velocity>();
        var chunks = query.Resolve(world);

        // Position+Velocity, Position+Velocity+Dead, Position+Velocity+BuffA — three distinct archetypes.
        chunks.Count.Should().Be(3);
    }

    [Fact]
    public void Access_ReadsCorrectComponentData_MatchingEntityOrder()
    {
        var world = BuildWorld(out var alive, out _, out _);

        var query = ArchetypeQuery.Empty.Has<Position>().Has<Velocity>().Without<Dead>().Without<BuffA>();
        var chunks = query.Resolve(world);

        chunks.Count.Should().Be(1);
        var chunk = chunks[0];
        chunk.Count.Should().Be(1);
        chunk.Entities[0].Should().Be(alive);

        var positions = chunk.Access<Ref<Position>>();
        positions[0].X.Should().Be(1f);
    }

    [Fact]
    public void Access_Mut_WritesAreVisibleThroughSubsequentAccess()
    {
        var world = BuildWorld(out _, out _, out _);

        var query = ArchetypeQuery.Empty.Has<Position>().Has<Velocity>().Without<Dead>().Without<BuffA>();
        var chunk = query.Resolve(world)[0];

        var positions = chunk.Access<Mut<Position>>();
        positions[0].X = 42f;

        chunk.Access<Ref<Position>>()[0].X.Should().Be(42f);
    }

    [Fact]
    public void Has_WithoutAccess_NarrowsMatchButAddsNoAccessibleData()
    {
        var world = BuildWorld(out _, out _, out var buffed);

        var query = ArchetypeQuery.Empty.Has<Position>().Has<BuffA>();
        var chunks = query.Resolve(world);

        chunks.Count.Should().Be(1);
        chunks[0].Entities[0].Should().Be(buffed);
    }

    [Fact]
    public void Any_RequiresAtLeastOneOfTheGiven()
    {
        var world = BuildWorld(out _, out _, out var buffed);

        var query = ArchetypeQuery.Empty.Has<Position>().Any<BuffA, BuffB>();
        var chunks = query.Resolve(world);

        chunks.Count.Should().Be(1);
        chunks[0].Entities[0].Should().Be(buffed);
    }

    [Fact]
    public void Empty_MatchesEveryArchetype()
    {
        var world = BuildWorld(out _, out _, out _);

        var total = 0;
        foreach (var chunk in ArchetypeQuery.Empty.Resolve(world))
            total += chunk.Count;

        total.Should().Be(3);
    }
}
