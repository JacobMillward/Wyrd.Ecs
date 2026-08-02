namespace Wyrd.Ecs.Tests;

public class SubscribeTagTests
{
    private struct Dead : ITag;
    private struct Position : IComponent { public float X; }

    [Fact]
    public void SubscribeTag_ReportsTagAdded()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        world.ApplyCommands();

        using var subscription = world.SubscribeTag<Dead>();
        world.Commands.AddTag<Dead>(a);
        world.ApplyCommands();

        var entries = subscription.Drain();

        entries.Should().ContainSingle();
        entries[0].Entity.Should().Be(a);
        entries[0].Kind.Should().Be(ChangeKind.TagAdded);
        entries[0].TypeIndex.Should().Be(Wyrd.Ecs.Internal.TypeIndex<Dead>.Value);
    }

    [Fact]
    public void SubscribeTag_ReportsTagRemoved()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        world.Commands.AddTag<Dead>(a);
        world.ApplyCommands();

        using var subscription = world.SubscribeTag<Dead>();
        world.Commands.RemoveTag<Dead>(a);
        world.ApplyCommands();

        var entries = subscription.Drain();

        entries.Should().ContainSingle();
        entries[0].Kind.Should().Be(ChangeKind.TagRemoved);
    }

    [Fact]
    public void SubscribeTag_DoesNotReportAnUnrelatedComponentChange()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        using var subscription = world.SubscribeTag<Dead>();
        world.GetComponent<Position>(a).X = 2f;
        world.AdvanceTick();
        world.Commands.CreateEntity();
        world.ApplyCommands();

        subscription.Drain().Should().BeEmpty();
    }

    [Fact]
    public void Dispose_StopsTagReporting()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        world.ApplyCommands();

        var subscription = world.SubscribeTag<Dead>();
        subscription.Dispose();

        world.Commands.AddTag<Dead>(a);
        world.ApplyCommands();

        var act = () => subscription.Drain();

        act.Should().Throw<KeyNotFoundException>();
    }
}
