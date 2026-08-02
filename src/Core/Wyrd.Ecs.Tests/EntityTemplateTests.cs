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
        template.Setters.Should().HaveCount(1, "the tag itself adds no setter");
    }

    [Fact]
    public void AddChild_InstantiatesTheWholeTree_WithParentEdges()
    {
        var world = new World();
        var child = new EntityTemplate().AddComponent(new Position());
        var root = new EntityTemplate().AddComponent(new Health { Value = 10 }).AddChild(child);

        Entity rootEntity = world.Commands.CreateEntity(root);
        world.ApplyCommands();

        var children = world.Sources<Parent>(rootEntity);
        children.Should().HaveCount(1);
        var childEntity = children.Single();
        world.HasComponent<Position>(childEntity).Should().BeTrue();
        world.Targets<Parent>(childEntity).Should().ContainKey(rootEntity);
    }

    [Fact]
    public void AddChild_SupportsMultipleLevelsOfNesting()
    {
        var world = new World();
        var grandchild = new EntityTemplate().AddComponent(new Position());
        var child = new EntityTemplate().AddComponent(new Health { Value = 1 }).AddChild(grandchild);
        var root = new EntityTemplate().AddChild(child);

        Entity rootEntity = world.Commands.CreateEntity(root);
        world.ApplyCommands();

        var childEntity = world.Sources<Parent>(rootEntity).Single();
        var grandchildEntity = world.Sources<Parent>(childEntity).Single();
        world.HasComponent<Position>(grandchildEntity).Should().BeTrue();
    }

    [Fact]
    public void AddChild_ReusesTheSameChildTemplateFromMultipleParents()
    {
        var world = new World();
        var shared = new EntityTemplate().AddComponent(new Position());
        var rootA = new EntityTemplate().AddChild(shared);
        var rootB = new EntityTemplate().AddChild(shared);

        Entity a = world.Commands.CreateEntity(rootA);
        Entity b = world.Commands.CreateEntity(rootB);
        world.ApplyCommands();

        world.Sources<Parent>(a).Single().Should().NotBe(world.Sources<Parent>(b).Single());
    }

    [Fact]
    public void DestroyingTheRootOfAnInstantiatedTree_CascadesThroughChildren()
    {
        var world = new World();
        var child = new EntityTemplate().AddComponent(new Position());
        var root = new EntityTemplate().AddChild(child);

        Entity rootEntity = world.Commands.CreateEntity(root);
        world.ApplyCommands();
        var childEntity = world.Sources<Parent>(rootEntity).Single();

        world.Commands.DestroyEntity(rootEntity);
        world.ApplyCommands();

        world.IsAlive(childEntity).Should().BeFalse();
    }

    [Fact]
    public void CreateEntity_FromTemplate_Batch_SharesOneArchetypeAndBlitsValues()
    {
        var world = new World();
        var template = new EntityTemplate().AddComponent(new Position { X = 7 });

        var entities = world.Commands.CreateEntity(template, 5);
        world.ApplyCommands();

        entities.Should().HaveCount(5);
        foreach (var e in entities) world.GetComponent<Position>(e).X.Should().Be(7);

        world.GetComponent<Position>(entities[0]) = new Position { X = 99 };
        world.GetComponent<Position>(entities[1]).X.Should().Be(7);
    }

    [Fact]
    public void CreateEntity_FromTemplate_BatchCountZero_ReturnsEmptyArray()
    {
        var world = new World();
        var template = new EntityTemplate().AddComponent(new Position());
        world.Commands.CreateEntity(template, 0).Should().BeEmpty();
    }

    [Fact]
    public void CreateEntity_FromTemplate_BatchNegativeCount_Throws()
    {
        var world = new World();
        var template = new EntityTemplate().AddComponent(new Position());
        var act = () => world.Commands.CreateEntity(template, -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateEntity_FromTemplate_BatchWithChildren_Throws()
    {
        var world = new World();
        var child = new EntityTemplate().AddComponent(new Position());
        var root = new EntityTemplate().AddChild(child);

        var act = () => world.Commands.CreateEntity(root, 3);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddParent_AttachesToAnAlreadyExistingEntity()
    {
        var world = new World();
        var existingParent = world.Commands.CreateEntity();
        world.ApplyCommands();

        var template = new EntityTemplate().AddComponent(new Position()).AddParent(existingParent);
        Entity child = world.Commands.CreateEntity(template);
        world.ApplyCommands();

        world.Targets<Parent>(child).Should().ContainKey(existingParent);
        world.Sources<Parent>(existingParent).Should().Contain(child);
    }

    [Fact]
    public void AddParent_OnADeadEntity_NoOps()
    {
        var world = new World();
        var deadParent = world.Commands.CreateEntity();
        world.ApplyCommands();
        world.Commands.DestroyEntity(deadParent);
        world.ApplyCommands();

        var template = new EntityTemplate().AddComponent(new Position()).AddParent(deadParent);
        Entity child = world.Commands.CreateEntity(template);
        world.ApplyCommands();

        world.IsAlive(child).Should().BeTrue();
        world.HasRelation<Parent>(child, deadParent).Should().BeFalse();
    }

    [Fact]
    public void AddParent_OnATemplateAlsoUsedAsAChild_Throws()
    {
        var world = new World();
        var existingParent = world.Commands.CreateEntity();
        world.ApplyCommands();

        var conflicted = new EntityTemplate().AddComponent(new Position()).AddParent(existingParent);
        var root = new EntityTemplate().AddChild(conflicted);

        // CreateEntity(EntityTemplate) returns EntityView, a ref struct, which can't be a
        // Func<T> type argument, so the block body makes this a plain Action instead.
        var act = () => { world.Commands.CreateEntity(root); };
        act.Should().Throw<InvalidOperationException>();
    }
}
