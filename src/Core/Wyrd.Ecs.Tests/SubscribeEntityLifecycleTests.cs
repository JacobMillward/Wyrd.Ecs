namespace Wyrd.Ecs.Tests;

public class SubscribeEntityLifecycleTests
{
    private struct Position : IComponent { public float X; }

    [Fact]
    public void ReportsEntityCreation()
    {
        var world = new World();
        using var subscription = world.SubscribeEntityLifecycle();

        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        var entries = subscription.Drain();

        entries.Should().ContainSingle();
        entries[0].Entity.Should().Be(a);
        entries[0].Kind.Should().Be(ChangeKind.EntityCreated);
        entries[0].TypeIndex.Should().BeNull();
    }

    [Fact]
    public void ReportsEntityDestruction()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        using var subscription = world.SubscribeEntityLifecycle();
        world.Commands.DestroyEntity(a);
        world.ApplyCommands();

        var entries = subscription.Drain();

        entries.Should().ContainSingle();
        entries[0].Kind.Should().Be(ChangeKind.EntityDestroyed);
    }

    [Fact]
    public void DoesNotReportComponentOrTagOrRelationEvents()
    {
        var world = new World();
        using var subscription = world.SubscribeEntityLifecycle();

        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();
        world.Commands.RemoveComponent<Position>(a);
        world.ApplyCommands();

        // Two CreateEntity calls above already produced two EntityCreated entries;
        // draining first isolates the assertion below to what happens next.
        subscription.Drain();

        world.Commands.AddComponent(b, new Position { X = 2f });
        world.ApplyCommands();

        subscription.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Dispose_StopsLifecycleReporting()
    {
        var world = new World();
        var subscription = world.SubscribeEntityLifecycle();
        subscription.Dispose();

        world.Commands.CreateEntity();
        world.ApplyCommands();

        var act = () => subscription.Drain();

        act.Should().Throw<KeyNotFoundException>();
    }
}
