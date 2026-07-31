namespace Wyrd.Ecs.Tests;

public class CommandsTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private struct Marker : ITag;

    private struct ConcurrentMarker : IComponent
    {
        public int Value;
    }

    [Fact]
    public void ConcurrentCreateEntity_FromMultipleThreads_EveryEntityGetsAUniqueIdAndSurvivesApply()
    {
        var world = new World();

        var entities = new Entity[500];
        Parallel.For(0, 500, i => entities[i] = world.Commands.CreateEntity().Entity);
        world.ApplyCommands();

        entities.Distinct().Should().HaveCount(500); // every reservation produced a unique entity, none clobbered by a race
        entities.Should().OnlyContain(e => world.IsAlive(e));
    }

    [Fact]
    public void ConcurrentCreateEntityWithComponent_FromMultipleThreads_EveryQueuedCommandSurvives()
    {
        var world = new World();

        var entities = new Entity[500];
        Parallel.For(0, 500, i => entities[i] = world.Commands.CreateEntity(new Position { X = i }).Entity);
        world.ApplyCommands();

        entities.Distinct().Should().HaveCount(500); // every reservation produced a unique entity, none clobbered by a race
        entities.Should().OnlyContain(e => world.IsAlive(e) && world.HasComponent<Position>(e));
    }

    [Fact]
    public void ConcurrentAddComponent_FromMultipleThreads_QueuesEveryCommandSafely()
    {
        var world = new World();
        var entities = Enumerable.Range(0, 500).Select(_ => world.Commands.CreateEntity().Entity).ToArray();
        world.ApplyCommands();

        Parallel.ForEach(entities, entity => world.Commands.AddComponent(entity, new ConcurrentMarker { Value = 1 }));
        world.ApplyCommands();

        var count = 0;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<ConcurrentMarker>>().Resolve(world))
            count += chunk.Count;
        count.Should().Be(500); // every one of the 500 concurrent enqueues must survive, none lost to a race
    }

    [Fact]
    public void CreateEntity_ReturnsARealEntityImmediately_ButItIsNotAliveYet()
    {
        var world = new World();

        var entity = world.Commands.CreateEntity().Entity;

        entity.IsNull.Should().BeFalse();
        world.IsAlive(entity).Should().BeFalse();
    }

    [Fact]
    public void CreateEntity_IsAliveAfterApply()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().Entity;

        world.ApplyCommands();

        world.IsAlive(entity).Should().BeTrue();
    }

    [Fact]
    public void CreateEntity_AccessingItBeforeApply_ThrowsNotAlive()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().Entity;

        var act = () => world.HasComponent<Position>(entity);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateEntity_ThenChainedAddComponent_BothApplyInOrder()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().Entity;
        world.Commands.AddComponent(entity, new Position { X = 9f });

        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeTrue();
        world.GetComponent<Position>(entity).X.Should().Be(9f);
    }

    [Fact]
    public void AddComponent_IsNotVisibleUntilApplied()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().Entity;
        world.ApplyCommands();

        world.Commands.AddComponent(entity, new Position { X = 5f });

        world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Fact]
    public void AddComponent_IsVisibleAfterApply()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().Entity;
        world.ApplyCommands();

        world.Commands.AddComponent(entity, new Position { X = 5f });
        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeTrue();
        world.GetComponent<Position>(entity).X.Should().Be(5f);
    }

    [Fact]
    public void RemoveComponent_IsAppliedInQueuedOrder()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position()).Entity;
        world.ApplyCommands();

        world.Commands.RemoveComponent<Position>(entity);
        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Fact]
    public void AddTag_IsAppliedOnApply()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().Entity;
        world.ApplyCommands();

        world.Commands.AddTag<Marker>(entity);
        world.ApplyCommands();

        world.HasTag<Marker>(entity).Should().BeTrue();
    }

    [Fact]
    public void RemoveTag_IsAppliedOnApply()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().Entity;
        world.Commands.AddTag<Marker>(entity);
        world.ApplyCommands();

        world.Commands.RemoveTag<Marker>(entity);
        world.ApplyCommands();

        world.HasTag<Marker>(entity).Should().BeFalse();
    }

    [Fact]
    public void DestroyEntity_IsAppliedOnApply()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().Entity;
        world.ApplyCommands();

        world.Commands.DestroyEntity(entity);
        world.ApplyCommands();

        world.IsAlive(entity).Should().BeFalse();
    }

    [Fact]
    public void ApplyCommands_ClearsTheQueue_SoASecondApplyDoesNothingExtra()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().Entity;
        world.Commands.AddComponent(entity, new Position { X = 1f });
        world.ApplyCommands();

        world.Commands.RemoveComponent<Position>(entity);
        world.ApplyCommands();
        world.HasComponent<Position>(entity).Should().BeFalse();

        world.Commands.AddComponent(entity, new Position { X = 2f });
        world.ApplyCommands();
        world.GetComponent<Position>(entity).X.Should().Be(2f);
    }

    [Fact]
    public void EarlierQueuedDestroy_MakesALaterQueuedAddComponent_SilentlyNotLand()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().Entity;
        world.ApplyCommands();

        world.Commands.DestroyEntity(entity);
        world.Commands.AddComponent(entity, new Position { X = 1f }); // queued after the destroy targeting the same entity

        var act = () => world.ApplyCommands();

        act.Should().NotThrow();
        world.IsAlive(entity).Should().BeFalse();
    }

    [Fact]
    public void EarlierQueuedDestroy_MakesALaterQueuedRemoveComponent_SilentlyNotLand()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position()).Entity;
        world.ApplyCommands();

        world.Commands.DestroyEntity(entity);
        world.Commands.RemoveComponent<Position>(entity);

        var act = () => world.ApplyCommands();

        act.Should().NotThrow();
    }

    [Fact]
    public void AddComponent_WhenTheEntityAlreadyHasIt_Overwrites()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position { X = 1f }).Entity;
        world.ApplyCommands();

        world.Commands.AddComponent(entity, new Position { X = 2f });

        var act = () => world.ApplyCommands();

        act.Should().NotThrow();
        world.GetComponent<Position>(entity).X.Should().Be(2f);
    }

    [Fact]
    public void AddComponent_QueuedTwiceForTheSameEntityInOneBatch_LastQueuedValueWins()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().Entity;
        world.ApplyCommands();

        world.Commands.AddComponent(entity, new Position { X = 1f });
        world.Commands.AddComponent(entity, new Position { X = 2f });
        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeTrue();
        world.GetComponent<Position>(entity).X.Should().Be(2f);
    }

    [Fact]
    public void QueuedStructuralChange_DuringQueryIteration_DoesNotCorruptTheIteration()
    {
        var world = new World();
        var toRemoveFrom = world.Commands.CreateEntity(new Position { X = 1f }).Entity;
        var untouched = world.Commands.CreateEntity(new Position { X = 2f }).Entity;
        world.ApplyCommands();

        var visited = new List<Entity>();
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Position>>().Resolve(world))
        {
            foreach (var entity in chunk.Entities)
            {
                visited.Add(entity);
                world.Commands.RemoveComponent<Position>(toRemoveFrom); // deferred: safe to queue mid-iteration
            }
        }

        visited.Should().BeEquivalentTo(new[] { toRemoveFrom, untouched });

        world.ApplyCommands();
        world.HasComponent<Position>(toRemoveFrom).Should().BeFalse();
        world.HasComponent<Position>(untouched).Should().BeTrue();
    }
}
