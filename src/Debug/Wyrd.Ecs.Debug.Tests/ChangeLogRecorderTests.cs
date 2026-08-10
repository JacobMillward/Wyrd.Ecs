using Wyrd.Ecs.Debug.Internal;

namespace Wyrd.Ecs.Debug.Tests;

public class ChangeLogRecorderTests
{
    [Fact]
    public void EntityCreatedAndDestroyed_AreRecorded()
    {
        var world = new World();
        var recorder = new ChangeLogRecorder(capacity: 10);
        using var handle = world.ObserveStructuralChanges(recorder);

        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();
        world.Commands.DestroyEntity(entity);
        world.ApplyCommands();

        recorder.Entries.Should().Contain(e => e.Kind == ChangeKind.EntityCreated && e.Entity == entity);
        recorder.Entries.Should().Contain(e => e.Kind == ChangeKind.EntityDestroyed && e.Entity == entity);
    }

    [Fact]
    public void ComponentAddedAndRemoved_AreRecorded()
    {
        var world = new World();
        var recorder = new ChangeLogRecorder(capacity: 10);
        using var handle = world.ObserveStructuralChanges(recorder);

        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();
        world.Commands.AddComponent(entity, new Position { X = 1f });
        world.ApplyCommands();
        world.Commands.RemoveComponent<Position>(entity);
        world.ApplyCommands();

        recorder.Entries.Should().Contain(e => e.Kind == ChangeKind.ComponentAdded && e.Entity == entity);
        recorder.Entries.Should().Contain(e => e.Kind == ChangeKind.ComponentRemoved && e.Entity == entity);
    }

    [Fact]
    public void NewestEntriesComeFirst()
    {
        var world = new World();
        var recorder = new ChangeLogRecorder(capacity: 10);
        using var handle = world.ObserveStructuralChanges(recorder);

        Entity first = world.Commands.CreateEntity();
        world.ApplyCommands();
        Entity second = world.Commands.CreateEntity();
        world.ApplyCommands();

        recorder.Entries[0].Entity.Should().Be(second);
        recorder.Entries[1].Entity.Should().Be(first);
    }

    [Fact]
    public void OverCapacity_DropsTheOldestEntry()
    {
        var world = new World();
        var recorder = new ChangeLogRecorder(capacity: 2);
        using var handle = world.ObserveStructuralChanges(recorder);

        Entity first = world.Commands.CreateEntity();
        world.ApplyCommands();
        Entity second = world.Commands.CreateEntity();
        world.ApplyCommands();
        Entity third = world.Commands.CreateEntity();
        world.ApplyCommands();

        recorder.Entries.Should().HaveCount(2);
        recorder.Entries.Should().NotContain(e => e.Entity == first);
        recorder.Entries.Should().Contain(e => e.Entity == second);
        recorder.Entries.Should().Contain(e => e.Entity == third);
    }
}
