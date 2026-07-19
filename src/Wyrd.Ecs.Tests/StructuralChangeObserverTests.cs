namespace Wyrd.Ecs.Tests;

public class StructuralChangeObserverTests
{
    private struct Position : IComponent;
    private struct Marker : ITag;

    private sealed class RecordingObserver : IStructuralChangeObserver
    {
        internal readonly List<string> Events = new();

        public void OnEntityCreated(Entity entity) => Events.Add($"Created:{entity.Id}");
        public void OnEntityDestroyed(Entity entity) => Events.Add($"Destroyed:{entity.Id}");
        public void OnComponentAdded(Entity entity, int typeIndex) => Events.Add($"ComponentAdded:{entity.Id}:{typeIndex}");
        public void OnComponentRemoved(Entity entity, int typeIndex) => Events.Add($"ComponentRemoved:{entity.Id}:{typeIndex}");
        public void OnTagAdded(Entity entity, int typeIndex) => Events.Add($"TagAdded:{entity.Id}:{typeIndex}");
        public void OnTagRemoved(Entity entity, int typeIndex) => Events.Add($"TagRemoved:{entity.Id}:{typeIndex}");
    }

    [Fact]
    public void CreateEntity_NotifiesEntityCreated()
    {
        var world = new World();
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        var entity = world.CreateEntity();

        observer.Events.Should().Equal($"Created:{entity.Id}");
    }

    [Fact]
    public void DestroyEntity_NotifiesEntityDestroyed()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.DestroyEntity(entity);

        observer.Events.Should().Equal($"Destroyed:{entity.Id}");
    }

    [Fact]
    public void AddComponent_NotifiesComponentAdded()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.AddComponent<Position>(entity);

        observer.Events.Should().Equal($"ComponentAdded:{entity.Id}:{Wyrd.Ecs.Internal.TypeIndex<Position>.Value}");
    }

    [Fact]
    public void RemoveComponent_NotifiesComponentRemoved()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.RemoveComponent<Position>(entity);

        observer.Events.Should().Equal($"ComponentRemoved:{entity.Id}:{Wyrd.Ecs.Internal.TypeIndex<Position>.Value}");
    }

    [Fact]
    public void RemoveComponent_WhenTheEntityDoesNotHaveIt_DoesNotNotify()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.RemoveComponent<Position>(entity); // no-op: entity never had it

        observer.Events.Should().BeEmpty();
    }

    [Fact]
    public void AddTag_NotifiesTagAdded()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.AddTag<Marker>(entity);

        observer.Events.Should().Equal($"TagAdded:{entity.Id}:{Wyrd.Ecs.Internal.TypeIndex<Marker>.Value}");
    }

    [Fact]
    public void RemoveTag_NotifiesTagRemoved()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddTag<Marker>(entity);
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.RemoveTag<Marker>(entity);

        observer.Events.Should().Equal($"TagRemoved:{entity.Id}:{Wyrd.Ecs.Internal.TypeIndex<Marker>.Value}");
    }

    [Fact]
    public void CreateEntityWithComponents_NotifiesOnlyEntityCreated_NotPerComponent()
    {
        var world = new World();
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        var entity = world.CreateEntity(new Position());

        observer.Events.Should().Equal($"Created:{entity.Id}");
    }

    [Fact]
    public void DisposingTheSubscription_StopsFurtherNotifications()
    {
        var world = new World();
        var observer = new RecordingObserver();
        var subscription = world.ObserveStructuralChanges(observer);
        subscription.Dispose();

        world.CreateEntity();

        observer.Events.Should().BeEmpty();
    }

    [Fact]
    public void QueuedAddComponent_NotifiesOnlyWhenApplied_NotWhenQueued()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.AddComponent(entity, new Position());
        observer.Events.Should().BeEmpty();

        world.ApplyCommands();

        observer.Events.Should().Equal($"ComponentAdded:{entity.Id}:{Wyrd.Ecs.Internal.TypeIndex<Position>.Value}");
    }

    [Fact]
    public void QueuedCommand_InvalidatedByAnEarlierCommand_NeverNotifies()
    {
        var world = new World();
        var entity = world.CreateEntity();
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        world.Commands.DestroyEntity(entity);
        world.Commands.AddComponent(entity, new Position());
        world.ApplyCommands();

        observer.Events.Should().Equal($"Destroyed:{entity.Id}"); // the AddComponent never landed, so it never notified
    }

    [Fact]
    public void CommandsCreateEntity_NotifiesOnlyOnApply_NotOnTheCreateEntityCall()
    {
        var world = new World();
        var observer = new RecordingObserver();
        using var subscription = world.ObserveStructuralChanges(observer);

        var entity = world.Commands.CreateEntity();
        observer.Events.Should().BeEmpty();

        world.ApplyCommands();

        observer.Events.Should().Equal($"Created:{entity.Id}");
    }
}
