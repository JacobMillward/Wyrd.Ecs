namespace Wyrd.Ecs.Tests;

public class RelationChangeObserverTests
{
    private struct Likes : IRelation
    {
        public float Weight;
    }

    private struct MarriedTo : IRelation, IExclusiveRelation;

    private sealed class RecordingObserver : IStructuralChangeObserver
    {
        public readonly List<(Entity Source, Entity Target, int TypeIndex)> Linked = [];
        public readonly List<(Entity Source, Entity Target, int TypeIndex)> Unlinked = [];

        public void OnEntityCreated(Entity entity) { }
        public void OnEntityDestroyed(Entity entity) { }
        public void OnComponentAdded(Entity entity, int typeIndex) { }
        public void OnComponentRemoved(Entity entity, int typeIndex) { }
        public void OnTagAdded(Entity entity, int typeIndex) { }
        public void OnTagRemoved(Entity entity, int typeIndex) { }
        public void OnRelationLinked(Entity source, Entity target, int typeIndex) => Linked.Add((source, target, typeIndex));
        public void OnRelationUnlinked(Entity source, Entity target, int typeIndex) => Unlinked.Add((source, target, typeIndex));
    }

    private sealed class MinimalObserver : IStructuralChangeObserver
    {
        public void OnEntityCreated(Entity entity) { }
        public void OnEntityDestroyed(Entity entity) { }
        public void OnComponentAdded(Entity entity, int typeIndex) { }
        public void OnComponentRemoved(Entity entity, int typeIndex) { }
        public void OnTagAdded(Entity entity, int typeIndex) { }
        public void OnTagRemoved(Entity entity, int typeIndex) { }
    }

    [Fact]
    public void AddRelation_NotifiesOnRelationLinked()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        observer.Linked.Should().ContainSingle()
            .Which.Should().Be((a, b, Wyrd.Ecs.Internal.TypeIndex<Likes>.Value));
    }

    [Fact]
    public void RemoveRelation_NotifiesOnRelationUnlinked()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.RemoveRelation<Likes>(a, b);
        world.ApplyCommands();

        observer.Unlinked.Should().ContainSingle()
            .Which.Should().Be((a, b, Wyrd.Ecs.Internal.TypeIndex<Likes>.Value));
    }

    [Fact]
    public void DestroyingSource_NotifiesOnRelationUnlinked()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.DestroyEntity(a);
        world.ApplyCommands();

        observer.Unlinked.Should().ContainSingle()
            .Which.Should().Be((a, b, Wyrd.Ecs.Internal.TypeIndex<Likes>.Value));
    }

    [Fact]
    public void DestroyingTarget_NotifiesOnRelationUnlinked()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
        world.ApplyCommands();

        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.DestroyEntity(b);
        world.ApplyCommands();

        observer.Unlinked.Should().ContainSingle()
            .Which.Should().Be((a, b, Wyrd.Ecs.Internal.TypeIndex<Likes>.Value));
    }

    [Fact]
    public void AddingExclusiveRelation_NotifiesUnlinkedForThePreviousTargetAndLinkedForTheNewOne()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        Entity c = world.Commands.CreateEntity();
        world.Commands.AddRelation<MarriedTo>(a, b);
        world.ApplyCommands();

        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.AddRelation<MarriedTo>(a, c);
        world.ApplyCommands();

        observer.Unlinked.Should().ContainSingle()
            .Which.Should().Be((a, b, Wyrd.Ecs.Internal.TypeIndex<MarriedTo>.Value));
        observer.Linked.Should().ContainSingle()
            .Which.Should().Be((a, c, Wyrd.Ecs.Internal.TypeIndex<MarriedTo>.Value));
    }

    [Fact]
    public void DestroyingTheRootOfAThreeLevelParentHierarchy_DestroysTheWholeSubtreeAndNotifiesEachEdgeUnlinkedExactlyOnce()
    {
        var world = new World();
        Entity grandparent = world.Commands.CreateEntity();
        Entity parent = world.Commands.CreateEntity();
        Entity child = world.Commands.CreateEntity();
        world.Commands.AddRelation<Parent>(parent, grandparent);
        world.Commands.AddRelation<Parent>(child, parent);
        world.ApplyCommands();

        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.DestroyEntity(grandparent);
        world.ApplyCommands();

        world.IsAlive(grandparent).Should().BeFalse();
        world.IsAlive(parent).Should().BeFalse();
        world.IsAlive(child).Should().BeFalse();

        observer.Unlinked.Should().HaveCount(2);
        observer.Unlinked.Should().Contain((parent, grandparent, Wyrd.Ecs.Internal.TypeIndex<Parent>.Value));
        observer.Unlinked.Should().Contain((child, parent, Wyrd.Ecs.Internal.TypeIndex<Parent>.Value));
    }

    [Fact]
    public void NotOverridingRelationCallbacks_StillCompilesAndDoesNothing()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        using var subscription = world.ObserveStructuralChanges(new MinimalObserver());

        var act = () =>
        {
            world.Commands.AddRelation(a, b, new Likes { Weight = 1f });
            world.ApplyCommands();
        };

        act.Should().NotThrow();
    }
}
