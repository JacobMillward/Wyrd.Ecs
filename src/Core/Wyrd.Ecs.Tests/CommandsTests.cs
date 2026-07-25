namespace Wyrd.Ecs.Tests;

public class CommandsTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private struct Marker : ITag;

    [Fact]
    public void CreateEntity_ReturnsARealEntityImmediately_ButItIsNotAliveYet()
    {
        var world = new World();

        var entity = world.Commands.CreateEntity();

        entity.IsNull.Should().BeFalse();
        world.IsAlive(entity).Should().BeFalse();
    }

    [Fact]
    public void CreateEntity_IsAliveAfterApply()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();

        world.ApplyCommands();

        world.IsAlive(entity).Should().BeTrue();
    }

    [Fact]
    public void CreateEntity_AccessingItBeforeApply_ThrowsNotAlive()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();

        var act = () => world.HasComponent<Position>(entity);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateEntity_ThenChainedAddComponent_BothApplyInOrder()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.Commands.AddComponent(entity, new Position { X = 9f });

        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeTrue();
        world.GetComponent<Position>(entity).X.Should().Be(9f);
    }

    [Fact]
    public void AddComponent_IsNotVisibleUntilApplied()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddComponent(entity, new Position { X = 5f });

        world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Fact]
    public void AddComponent_IsVisibleAfterApply()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
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
        var entity = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();

        world.Commands.RemoveComponent<Position>(entity);
        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeFalse();
    }

    [Fact]
    public void AddTag_IsAppliedOnApply()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.AddTag<Marker>(entity);
        world.ApplyCommands();

        world.HasTag<Marker>(entity).Should().BeTrue();
    }

    [Fact]
    public void RemoveTag_IsAppliedOnApply()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
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
        var entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.DestroyEntity(entity);
        world.ApplyCommands();

        world.IsAlive(entity).Should().BeFalse();
    }

    [Fact]
    public void ApplyCommands_ClearsTheQueue_SoASecondApplyDoesNothingExtra()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity();
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
        var entity = world.Commands.CreateEntity();
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
        var entity = world.Commands.CreateEntity(new Position());
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
        var entity = world.Commands.CreateEntity(new Position { X = 1f });
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
        var entity = world.Commands.CreateEntity();
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
        var toRemoveFrom = world.Commands.CreateEntity(new Position { X = 1f });
        var untouched = world.Commands.CreateEntity(new Position { X = 2f });
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
