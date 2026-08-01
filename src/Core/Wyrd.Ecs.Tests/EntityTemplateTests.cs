namespace Wyrd.Ecs.Tests;

public class EntityTemplateTests
{
    // Matches the existing per-test-class nested component convention (see e.g.
    // BatchEntityCreationTests.cs) rather than a shared cross-file component type.
    private struct Position : IComponent
    {
        public float X;
        public float Y;
    }

    private struct Health : IComponent
    {
        public int Value;
    }

    [Fact]
    public void AddComponent_AccumulatesSignatureBits()
    {
        var template = new EntityTemplate()
            .AddComponent(new Position { X = 1, Y = 2 })
            .AddComponent(new Health { Value = 100 });

        template.Signature.Contains(Wyrd.Ecs.Internal.TypeIndex<Position>.Value).Should().BeTrue();
        template.Signature.Contains(Wyrd.Ecs.Internal.TypeIndex<Health>.Value).Should().BeTrue();
    }

    [Fact]
    public void AddComponent_CalledTwiceForSameType_LastValueWins()
    {
        var template = new EntityTemplate()
            .AddComponent(new Health { Value = 1 })
            .AddComponent(new Health { Value = 2 });

        template.Setters.Should().HaveCount(1);
    }

    [Fact]
    public void CreateEntity_FromTemplate_PlacesEntityWithComponentValues()
    {
        var world = new World();
        var template = new EntityTemplate()
            .AddComponent(new Position { X = 3, Y = 4 })
            .AddComponent(new Health { Value = 50 });

        Entity entity = world.Commands.CreateEntity(template);
        world.ApplyCommands();

        world.IsAlive(entity).Should().BeTrue();
        world.GetComponent<Position>(entity).X.Should().Be(3);
        world.GetComponent<Health>(entity).Value.Should().Be(50);
    }

    [Fact]
    public void CreateEntity_FromTemplate_TwoEntitiesOfTheSameTemplate_ShareOneArchetype()
    {
        var world = new World();
        var template = new EntityTemplate().AddComponent(new Position());

        Entity a = world.Commands.CreateEntity(template);
        Entity b = world.Commands.CreateEntity(template);
        world.ApplyCommands();

        world.GetComponent<Position>(a) = new Position { X = 1 };
        world.GetComponent<Position>(b) = new Position { X = 2 };
        world.GetComponent<Position>(a).X.Should().Be(1);
        world.GetComponent<Position>(b).X.Should().Be(2);
    }

    [Fact]
    public void CreateEntity_FromEmptyTemplate_CreatesALiveEntityWithNoComponents()
    {
        var world = new World();
        var template = new EntityTemplate();

        Entity entity = world.Commands.CreateEntity(template);
        world.ApplyCommands();

        world.IsAlive(entity).Should().BeTrue();
        world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Fact]
    public void CreateEntity_FromTemplate_WithTrackingOn_MarksTheRowDirty()
    {
        var world = new World();
        using var tracking = world.TrackChanges<Position>();
        var sinceTick = world.CurrentTick;
        world.AdvanceTick(); // entries recorded at or before sinceTick are never visible
        var template = new EntityTemplate().AddComponent(new Position());

        Entity entity = world.Commands.CreateEntity(template);
        world.ApplyCommands();

        var seen = new List<Entity>();
        foreach (var change in world.ReadChanges<Position>(sinceTick)) seen.Add(change.Entity);
        seen.Should().Contain(entity);
    }

    [Fact]
    public void CreateEntity_FromTemplate_WithTrackingOff_MarksNothingDirty()
    {
        var world = new World();
        var sinceTick = world.CurrentTick;
        world.AdvanceTick();
        var template = new EntityTemplate().AddComponent(new Position());

        Entity entity = world.Commands.CreateEntity(template);
        world.ApplyCommands();

        var seen = new List<Entity>();
        foreach (var change in world.ReadChanges<Position>(sinceTick)) seen.Add(change.Entity);
        seen.Should().NotContain(entity);
    }

    private struct Hostile : ITag;

    [Fact]
    public void AddTag_ContributesSignatureBitButNoStorage()
    {
        var world = new World();
        var template = new EntityTemplate()
            .AddComponent(new Position())
            .AddTag<Hostile>();

        Entity entity = world.Commands.CreateEntity(template);
        world.ApplyCommands();

        world.HasTag<Hostile>(entity).Should().BeTrue();
        template.Setters.Should().HaveCount(1); // the tag itself adds no setter
    }
}
