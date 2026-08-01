namespace Wyrd.Ecs.Tests;

public class WorldDebugTests
{
    private struct Position : IComponent { public float X; }
    private struct Velocity : IComponent { public float X; }
    private struct Enemy : ITag { }

    private static ComponentCodecRegistry NewRegistry()
    {
        var registry = new ComponentCodecRegistry();
        registry.Register<Position>("Position", p => BitConverter.GetBytes(p.X), b => new Position { X = BitConverter.ToSingle(b) });
        registry.Register<Velocity>("Velocity", v => BitConverter.GetBytes(v.X), b => new Velocity { X = BitConverter.ToSingle(b) });
        registry.RegisterTag<Enemy>("Enemy");
        return registry;
    }

    [Fact]
    public void EnumerateArchetypes_OnAnEmptyWorld_YieldsNothing()
    {
        var world = new World();

        world.EnumerateArchetypes(NewRegistry()).Should().BeEmpty();
    }

    [Fact]
    public void EnumerateArchetypes_ReportsEntityCountAndComponentDiscriminatorsForOneArchetype()
    {
        var world = new World();
        world.Commands.CreateEntity(new Position { X = 1f }, new Velocity { X = 2f });
        world.Commands.CreateEntity(new Position { X = 3f }, new Velocity { X = 4f });
        world.ApplyCommands();

        var snapshot = world.EnumerateArchetypes(NewRegistry()).Should().ContainSingle().Subject;

        snapshot.EntityCount.Should().Be(2);
        snapshot.ComponentDiscriminators.Should().BeEquivalentTo(["Position", "Velocity"]);
        snapshot.TagDiscriminators.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateArchetypes_ReportsTagDiscriminators()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position { X = 1f });
        world.Commands.AddTag<Enemy>(entity);
        world.ApplyCommands();

        var snapshot = world.EnumerateArchetypes(NewRegistry()).Should().ContainSingle().Subject;

        snapshot.TagDiscriminators.Should().BeEquivalentTo(["Enemy"]);
    }

    [Fact]
    public void EnumerateArchetypes_WithAnUnregisteredComponentType_SkipsItSilently()
    {
        var world = new World();
        world.Commands.CreateEntity(new Velocity { X = 1f }); // not registered by NewRegistryWithOnlyPosition below
        world.ApplyCommands();

        var registry = new ComponentCodecRegistry();
        registry.Register<Position>("Position", p => BitConverter.GetBytes(p.X), b => new Position { X = BitConverter.ToSingle(b) });

        var snapshot = world.EnumerateArchetypes(registry).Should().ContainSingle().Subject;

        snapshot.ComponentDiscriminators.Should().BeEmpty();
        snapshot.EntityCount.Should().Be(1);
    }

    [Fact]
    public void EnumerateArchetypes_WithSeveralDistinctArchetypes_ReportsOneSnapshotEach()
    {
        var world = new World();
        world.Commands.CreateEntity(new Position { X = 1f });
        world.Commands.CreateEntity(new Position { X = 2f }, new Velocity { X = 3f });
        world.ApplyCommands();

        var snapshots = world.EnumerateArchetypes(NewRegistry()).ToList();

        snapshots.Should().HaveCount(2);
        snapshots.Sum(s => s.EntityCount).Should().Be(2);
    }

    [Fact]
    public void EnumerateArchetypes_IsUnaffectedByAMutationThatReachesANewArchetypeAfterItReturns()
    {
        // Regression test for the eager-materialization decision: EnumerateArchetypes
        // must fully snapshot before returning, so a structural mutation applied after it
        // returns (but before the caller finishes reading the result) must never throw
        // "Collection was modified" against the live World.
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        var snapshots = world.EnumerateArchetypes(NewRegistry());

        world.Commands.AddComponent(entity, new Velocity { X = 2f }); // moves entity to a brand-new archetype
        world.ApplyCommands();

        snapshots.Should().ContainSingle().Which.EntityCount.Should().Be(1);
    }
}
