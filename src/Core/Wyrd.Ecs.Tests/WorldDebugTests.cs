namespace Wyrd.Ecs.Tests;

// Public, since DebugNameGenerator only registers accessible types - every enumeration test
// here needs a debug name that's actually been auto-registered, whether or not it's also
// registered for byte-payload encoding.
public struct DebugPosition : IComponent { public float X; }
public struct DebugVelocity : IComponent { public float X; }
public struct DebugEnemyTag : ITag { }

public class WorldDebugTests
{
    private static CodecRegistry NewRegistry()
    {
        var registry = new CodecRegistry();
        registry.Register<DebugPosition>("Position", p => BitConverter.GetBytes(p.X), b => new DebugPosition { X = BitConverter.ToSingle(b) });
        registry.Register<DebugVelocity>("Velocity", v => BitConverter.GetBytes(v.X), b => new DebugVelocity { X = BitConverter.ToSingle(b) });
        return registry;
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
        world.Commands.CreateEntity(new DebugPosition { X = 1f }, new DebugVelocity { X = 2f });
        world.ApplyCommands();

        var snapshot = world.EnumerateEntities(NewRegistry()).Should().ContainSingle().Subject;

        snapshot.Components.Select(c => c.Discriminator).Should().BeEquivalentTo([nameof(DebugPosition), nameof(DebugVelocity)]);
    }

    [Fact]
    public void EnumerateEntities_YieldsTagDiscriminatorsForTheEntity()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new DebugPosition { X = 1f });
        world.Commands.AddTag<DebugEnemyTag>(entity);
        world.ApplyCommands();

        var snapshot = world.EnumerateEntities(NewRegistry()).Should().ContainSingle().Subject;

        snapshot.Tags.Should().BeEquivalentTo([nameof(DebugEnemyTag)]);
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
    public void EnumerateEntities_WithAnUnregisteredComponentType_StillAppearsByName_WithEmptyData()
    {
        var world = new World();
        world.Commands.CreateEntity(new DebugVelocity { X = 1f });
        world.ApplyCommands();

        var registry = new CodecRegistry();
        registry.Register<DebugPosition>("Position", p => BitConverter.GetBytes(p.X), b => new DebugPosition { X = BitConverter.ToSingle(b) });

        var snapshot = world.EnumerateEntities(registry).Should().ContainSingle().Subject;

        var component = snapshot.Components.Should().ContainSingle().Subject;
        component.Discriminator.Should().Be(nameof(DebugVelocity));
        component.Data.Should().BeEmpty();
    }

    [Fact]
    public void EnumerateEntities_ReportsCorrectEntityIdentityAcrossMultipleEntities()
    {
        var world = new World();
        var a = world.Commands.CreateEntity(new DebugPosition { X = 1f });
        var b = world.Commands.CreateEntity(new DebugPosition { X = 2f });
        world.ApplyCommands();

        var snapshots = world.EnumerateEntities(NewRegistry()).ToList();

        snapshots.Select(s => s.Entity).Should().BeEquivalentTo([(Entity)a, (Entity)b]);
    }

    [Fact]
    public void EnumerateEntities_IsUnaffectedByAMutationThatReachesANewArchetypeAfterItReturns()
    {
        // A structural mutation applied after EnumerateEntities returns (but before the
        // caller finishes reading the result) must never throw "Collection was modified"
        // against the live World.
        var world = new World();
        var entity = world.Commands.CreateEntity(new DebugPosition { X = 1f });
        world.ApplyCommands();

        var snapshots = world.EnumerateEntities(NewRegistry());

        world.Commands.AddComponent(entity, new DebugVelocity { X = 2f }); // moves entity to a brand-new archetype
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
