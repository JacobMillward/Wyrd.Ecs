namespace Wyrd.Ecs.Tests;

public class SubscribeComponentAddRemoveTests
{
    private struct Position : IComponent { public float X; }
    private struct Velocity : IComponent { public float X; }

    [Fact]
    public void Subscribe_ReportsComponentAddedForAnAlreadyExistingEntity()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        world.ApplyCommands();

        using var subscription = world.Subscribe<Position>();
        world.Commands.AddComponent(a, new Position { X = 1f });
        world.ApplyCommands();

        var entries = subscription.Drain();

        entries.Should().ContainSingle();
        entries[0].Entity.Should().Be(a);
        entries[0].Kind.Should().Be(ChangeKind.ComponentAdded);
    }

    [Fact]
    public void Subscribe_ReportsComponentRemoved()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        using var subscription = world.Subscribe<Position>();
        world.Commands.RemoveComponent<Position>(a);
        world.ApplyCommands();

        var entries = subscription.Drain();

        entries.Should().ContainSingle();
        entries[0].Kind.Should().Be(ChangeKind.ComponentRemoved);
    }

    [Fact]
    public void Subscribe_DoesNotReportAnUnrelatedComponentTypesAddOrRemove()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        world.ApplyCommands();

        using var subscription = world.Subscribe<Position>();
        world.Commands.AddComponent(a, new Velocity { X = 1f });
        world.ApplyCommands();

        subscription.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Subscribe_DoesNotReportEntityCreation()
    {
        var world = new World();
        using var subscription = world.Subscribe<Position>();

        world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        subscription.Drain().Should().BeEmpty();
    }
}
