namespace Wyrd.Ecs.Tests;

// Public (unlike the fixtures nested in WorldDebugTests below), since DebugNameGenerator
// only registers accessible types - the zero-arg overloads need a debug name that's
// actually been auto-registered.
public struct DebugPosition : IComponent { public float X; }
public struct DebugEnemyTag : ITag { }

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
        // EnumerateArchetypes must fully snapshot before returning, so a structural mutation
        // applied after it returns must never affect the already-captured result.
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        var snapshots = world.EnumerateArchetypes(NewRegistry());

        world.Commands.AddComponent(entity, new Velocity { X = 2f }); // moves entity to a brand-new archetype
        world.ApplyCommands();

        snapshots.Should().ContainSingle().Which.EntityCount.Should().Be(1);
    }

    [Fact]
    public void EnumerateEntities_OnAnEmptyWorld_YieldsNothing()
    {
        var world = new World();

        world.EnumerateEntities(NewRegistry()).Should().BeEmpty();
    }

    [Fact]
    public void EnumerateEntities_YieldsOneSnapshotPerLiveEntityWithItsComponents()
    {
        var world = new World();
        world.Commands.CreateEntity(new Position { X = 1f }, new Velocity { X = 2f });
        world.ApplyCommands();

        var snapshot = world.EnumerateEntities(NewRegistry()).Should().ContainSingle().Subject;

        snapshot.Components.Select(c => c.Discriminator).Should().BeEquivalentTo(["Position", "Velocity"]);
    }

    [Fact]
    public void EnumerateEntities_YieldsTagDiscriminatorsForTheEntity()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position { X = 1f });
        world.Commands.AddTag<Enemy>(entity);
        world.ApplyCommands();

        var snapshot = world.EnumerateEntities(NewRegistry()).Should().ContainSingle().Subject;

        snapshot.Tags.Should().BeEquivalentTo(["Enemy"]);
    }

    [Fact]
    public void EnumerateEntities_IncludesAnEntityWithNoRegisteredComponentsOrTags_UnlikeEnumerateAll()
    {
        var world = new World();
        world.Commands.CreateEntity(); // no components, no tags at all
        world.ApplyCommands();

        var registry = NewRegistry();

        world.EnumerateAll(registry).Should().BeEmpty("the existing method silently drops this entity");
        var snapshot = world.EnumerateEntities(registry).Should().ContainSingle().Subject;
        snapshot.Components.Should().BeEmpty();
        snapshot.Tags.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateEntities_WithAnUnregisteredComponentType_OmitsItFromTheSnapshotButStillYieldsTheEntity()
    {
        var world = new World();
        world.Commands.CreateEntity(new Velocity { X = 1f });
        world.ApplyCommands();

        var registry = new ComponentCodecRegistry();
        registry.Register<Position>("Position", p => BitConverter.GetBytes(p.X), b => new Position { X = BitConverter.ToSingle(b) });

        var snapshot = world.EnumerateEntities(registry).Should().ContainSingle().Subject;

        snapshot.Components.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateEntities_ReportsCorrectEntityIdentityAcrossMultipleEntities()
    {
        var world = new World();
        var a = world.Commands.CreateEntity(new Position { X = 1f });
        var b = world.Commands.CreateEntity(new Position { X = 2f });
        world.ApplyCommands();

        var snapshots = world.EnumerateEntities(NewRegistry()).ToList();

        snapshots.Select(s => s.Entity).Should().BeEquivalentTo([(Entity)a, (Entity)b]);
    }

    [Fact]
    public void EnumerateEntities_IsUnaffectedByAMutationThatReachesANewArchetypeAfterItReturns()
    {
        // Same regression as EnumerateArchetypes' equivalent test: a structural mutation
        // applied after EnumerateEntities returns (but before the caller finishes reading the
        // result) must never throw "Collection was modified" against the live World.
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        var snapshots = world.EnumerateEntities(NewRegistry());

        world.Commands.AddComponent(entity, new Velocity { X = 2f }); // moves entity to a brand-new archetype
        world.ApplyCommands();

        snapshots.Should().ContainSingle().Which.Components.Should().HaveCount(1, "the pre-mutation snapshot: Position only");
    }

    [Fact]
    public void EnumerateArchetypes_ZeroArg_NeedsNoRegistry()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new DebugPosition());
        world.Commands.AddTag<DebugEnemyTag>(entity);
        world.ApplyCommands();

        var snapshot = world.EnumerateArchetypes().Should().ContainSingle().Subject;

        snapshot.EntityCount.Should().Be(1);
        snapshot.ComponentDiscriminators.Should().BeEquivalentTo([nameof(DebugPosition)]);
        snapshot.TagDiscriminators.Should().BeEquivalentTo([nameof(DebugEnemyTag)]);
    }

    [Fact]
    public void EnumerateEntities_ZeroArg_ComponentWithNoCodec_ShowsNameWithEmptyData()
    {
        var world = new World();
        world.Commands.CreateEntity(new DebugPosition());
        world.ApplyCommands();

        var entities = world.EnumerateEntities();

        var component = entities.Should().ContainSingle().Subject.Components.Should().ContainSingle().Subject;
        component.Discriminator.Should().Be(nameof(DebugPosition));
        component.Data.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateEntities_ZeroArg_IncludesAnEntityWithNoComponentsOrTags()
    {
        var world = new World();
        world.Commands.CreateEntity();
        world.ApplyCommands();

        var snapshot = world.EnumerateEntities().Should().ContainSingle().Subject;

        snapshot.Components.Should().BeEmpty();
        snapshot.Tags.Should().BeEmpty();
    }
}
