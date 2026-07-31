namespace Wyrd.Ecs.Tests;

public class BatchEntityCreationTests
{
    [Fact]
    public void CreateEntity_Bare_ReturnsCountDistinctEntities()
    {
        var world = new World();

        var entities = world.Commands.CreateEntity(5);
        world.ApplyCommands();

        entities.Should().HaveCount(5);
        entities.Distinct().Should().HaveCount(5);
        entities.Should().OnlyContain(e => world.IsAlive(e));
    }

    [Fact]
    public void CreateEntity_Bare_ZeroCount_ReturnsEmptyArrayAndQueuesNothing()
    {
        var world = new World();

        var entities = world.Commands.CreateEntity(0);
        var act = () => world.ApplyCommands();

        entities.Should().BeEmpty();
        act.Should().NotThrow();
    }

    [Fact]
    public void CreateEntity_Bare_NegativeCount_ThrowsImmediately()
    {
        var world = new World();

        var act = () => world.Commands.CreateEntity(-1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateEntity_Bare_NotAliveUntilApplied()
    {
        var world = new World();

        var entities = world.Commands.CreateEntity(3);

        entities.Should().OnlyContain(e => !world.IsAlive(e));
    }

    private struct Position : IComponent
    {
        public float X;
    }

    private struct Velocity : IComponent
    {
        public float X;
    }

    private sealed class RecordingObserver : IStructuralChangeObserver
    {
        public int CreatedCount;
        public void OnEntityCreated(Entity entity) => CreatedCount++;
        public void OnEntityDestroyed(Entity entity) { }
        public void OnComponentAdded(Entity entity, int typeIndex) { }
        public void OnComponentRemoved(Entity entity, int typeIndex) { }
        public void OnTagAdded(Entity entity, int typeIndex) { }
        public void OnTagRemoved(Entity entity, int typeIndex) { }
    }

    [Fact]
    public void CreateEntity_WithOneComponent_Batch_EveryEntityGetsTheSameValue()
    {
        var world = new World();

        var entities = world.Commands.CreateEntity(4, new Position { X = 5f });
        world.ApplyCommands();

        entities.Should().HaveCount(4);
        entities.Should().OnlyContain(e => world.HasComponent<Position>(e));
        entities.All(e => world.GetComponent<Position>(e).X == 5f).Should().BeTrue();
    }

    [Fact]
    public void CreateEntity_WithTwoComponents_Batch_EveryEntityGetsBothValues()
    {
        var world = new World();

        var entities = world.Commands.CreateEntity(3, new Position { X = 1f }, new Velocity { X = 2f });
        world.ApplyCommands();

        entities.All(e => world.GetComponent<Position>(e).X == 1f).Should().BeTrue();
        entities.All(e => world.GetComponent<Velocity>(e).X == 2f).Should().BeTrue();
    }

    [Fact]
    public void CreateEntity_WithComponents_Batch_AllEntitiesShareOneArchetype()
    {
        var world = new World();

        var entities = world.Commands.CreateEntity(5, new Position { X = 1f });
        world.ApplyCommands();

        var (firstArchetype, _) = TestReflection.GetLocation(world, entities[0]);
        entities.Should().OnlyContain(e => TestReflection.GetLocation(world, e).Archetype == firstArchetype);
    }

    [Fact]
    public void CreateEntity_WithComponents_Batch_ZeroCount_ReturnsEmptyArray()
    {
        var world = new World();

        var entities = world.Commands.CreateEntity(0, new Position { X = 1f });

        entities.Should().BeEmpty();
    }

    [Fact]
    public void CreateEntity_WithComponents_Batch_NegativeCount_ThrowsImmediately()
    {
        var world = new World();

        var act = () => world.Commands.CreateEntity(-1, new Position());

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CreateEntity_WithComponents_Batch_MutatingOneEntityDoesNotAffectOthers()
    {
        var world = new World();

        var entities = world.Commands.CreateEntity(3, new Position { X = 1f });
        world.ApplyCommands();

        world.GetComponent<Position>(entities[1]).X = 99f;

        world.GetComponent<Position>(entities[0]).X.Should().Be(1f);
        world.GetComponent<Position>(entities[2]).X.Should().Be(1f);
    }

    [Fact]
    public void CreateEntity_WithComponents_Batch_WithTrackingOn_MarksEveryRowDirtyAtTheCurrentTick()
    {
        var world = new World();
        using var consumer = world.TrackChanges<Position>();

        var entities = world.Commands.CreateEntity(3, new Position());
        world.ApplyCommands();

        foreach (var entity in entities)
        {
            var (archetype, row) = TestReflection.GetLocation(world, entity);
            var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
            storage.RawLastMarkedTick[row].Should().Be(world.CurrentTick);
        }
    }

    [Fact]
    public void CreateEntity_WithComponents_Batch_WithTrackingOff_NeverMarksDirty()
    {
        var world = new World();

        var entities = world.Commands.CreateEntity(3, new Position());
        world.ApplyCommands();

        foreach (var entity in entities)
        {
            var (archetype, row) = TestReflection.GetLocation(world, entity);
            var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
            storage.RawLastMarkedTick[row].Should().Be(0);
        }
    }

    [Fact]
    public void CreateEntity_Batch_ThenDestroyOneOfTheReturnedEntities_RunsInQueueOrder()
    {
        var world = new World();

        var entities = world.Commands.CreateEntity(5, new Position());
        world.Commands.DestroyEntity(entities[2]);
        world.ApplyCommands();

        world.IsAlive(entities[2]).Should().BeFalse();
        world.IsAlive(entities[0]).Should().BeTrue();
        world.IsAlive(entities[4]).Should().BeTrue();
    }

    [Fact]
    public void CreateEntity_WithComponents_Batch_NotifiesObserverOncePerEntity()
    {
        var world = new World();
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.CreateEntity(7, new Position());
        world.ApplyCommands();

        observer.CreatedCount.Should().Be(7);
    }

    [Fact]
    public void CreateEntity_WithComponents_Batch_AndSingleEntityCreateEntity_ShareOneArchetype()
    {
        var world = new World();

        Entity single = world.Commands.CreateEntity(new Position { X = 1f });
        var batch = world.Commands.CreateEntity(3, new Position { X = 2f });
        world.ApplyCommands();

        var (singleArchetype, _) = TestReflection.GetLocation(world, single);
        batch.Should().OnlyContain(e => TestReflection.GetLocation(world, e).Archetype == singleArchetype);
    }

    [Fact]
    public void ConcurrentBatchCreateEntity_FromMultipleThreads_EveryEntityGetsAUniqueIdAndSurvivesApply()
    {
        var world = new World();
        var batches = new Entity[10][];

        Parallel.For(0, 10, i => batches[i] = world.Commands.CreateEntity(50, new Position { X = i }));
        world.ApplyCommands();

        var all = batches.SelectMany(b => b).ToArray();
        all.Should().HaveCount(500);
        all.Distinct().Should().HaveCount(500);
        all.Should().OnlyContain(e => world.IsAlive(e));
    }
}
