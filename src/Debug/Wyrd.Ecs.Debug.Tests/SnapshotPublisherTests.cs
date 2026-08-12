using Wyrd.Ecs.Debug.Internal;

namespace Wyrd.Ecs.Debug.Tests;

public struct Position : IComponent { public float X; }

public class SnapshotPublisherTests
{
    [Fact]
    public void WithNoSubscriber_OnTickAdvancedDoesNoWork()
    {
        var world = new World();
        var registry = new CodecRegistry();
        var publisher = new SnapshotPublisher(world, registry);

        publisher.OnTickAdvanced(1);

        publisher.Latest.Should().BeNull();
    }

    [Fact]
    public void WithASubscriber_OnTickAdvancedPublishesASnapshot()
    {
        var world = new World();
        var registry = new CodecRegistry();
        registry.Register<Position>("Position", p => BitConverter.GetBytes(p.X), b => new Position { X = BitConverter.ToSingle(b) });
        world.Commands.CreateEntity(new Position { X = 3f });
        world.ApplyCommands();

        var publisher = new SnapshotPublisher(world, registry);
        publisher.Changed += () => { };

        publisher.OnTickAdvanced(1);

        publisher.Latest.Should().NotBeNull();
        publisher.Latest!.Archetypes.Should().ContainSingle(a => a.ComponentDiscriminators.Contains("Position"));
        publisher.Latest!.Entities.Should().ContainSingle(e => e.Components.Any(c => c.Discriminator == "Position"));
    }

    [Fact]
    public void AfterTheLastSubscriberUnsubscribes_OnTickAdvancedStopsPublishingNewSnapshots()
    {
        var world = new World();
        var registry = new CodecRegistry();
        var publisher = new SnapshotPublisher(world, registry);
        Action subscriber = () => { };
        publisher.Changed += subscriber;
        publisher.OnTickAdvanced(1);
        var firstSnapshot = publisher.Latest;

        publisher.Changed -= subscriber;
        world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        publisher.OnTickAdvanced(2);

        publisher.Latest.Should().BeSameAs(firstSnapshot);
    }

    [Fact]
    public void TwoSubscribers_OneUnsubscribingKeepsPublishing()
    {
        var world = new World();
        var registry = new CodecRegistry();
        var publisher = new SnapshotPublisher(world, registry);
        Action first = () => { };
        Action second = () => { };
        publisher.Changed += first;
        publisher.Changed += second;

        publisher.Changed -= first;
        publisher.OnTickAdvanced(1);

        publisher.Latest.Should().NotBeNull();
    }

    [Fact]
    public void WithASubscriber_OnTickAdvancedRaisesChanged()
    {
        var world = new World();
        var publisher = new SnapshotPublisher(world, new CodecRegistry());
        var raised = false;
        publisher.Changed += () => raised = true;

        publisher.OnTickAdvanced(1);

        raised.Should().BeTrue();
    }
}
