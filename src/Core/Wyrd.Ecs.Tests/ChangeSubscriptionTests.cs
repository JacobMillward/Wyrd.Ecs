namespace Wyrd.Ecs.Tests;

public class ChangeSubscriptionTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    [Fact]
    public void Subscribe_ReportsAValueChangeAfterTheNextTickAdvance()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        using var subscription = world.Subscribe<Position>();

        world.GetComponent<Position>(a).X = 2f;
        world.AdvanceTick();

        var entries = subscription.Drain();

        entries.Should().ContainSingle();
        entries[0].Entity.Should().Be(a);
        entries[0].Related.Should().Be(Entity.Null);
        entries[0].Kind.Should().Be(ChangeKind.ValueChanged);
        entries[0].TypeIndex.Should().Be(Wyrd.Ecs.Internal.TypeIndex<Position>.Value);
    }

    [Fact]
    public void Drain_ClearsTheBufferSoASecondDrainIsEmpty()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        using var subscription = world.Subscribe<Position>();

        world.GetComponent<Position>(a).X = 2f;
        world.AdvanceTick();
        subscription.Drain();

        var second = subscription.Drain();

        second.Should().BeEmpty();
    }

    [Fact]
    public void ChangesBeforeSubscribing_AreNotReported()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        world.GetComponent<Position>(a).X = 2f;
        world.AdvanceTick();

        using var subscription = world.Subscribe<Position>();
        world.AdvanceTick();

        subscription.Drain().Should().BeEmpty();
    }

    /// <summary>
    /// Subscribe must watermark at <c>CurrentTick - 1</c>, not <c>CurrentTick</c>: ticks are
    /// coarse, so a same-tick mutation after Subscribe would otherwise be missed by the first scan.
    /// </summary>
    [Fact]
    public void MutationInTheSameTickAsSubscribing_IsStillReported()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        world.AdvanceTick();

        using var subscription = world.Subscribe<Position>();
        world.GetComponent<Position>(a).X = 2f;
        world.AdvanceTick();

        var entries = subscription.Drain();

        entries.Should().ContainSingle();
        entries[0].Entity.Should().Be(a);
    }

    [Fact]
    public void Dispose_StopsFurtherReporting()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        var subscription = world.Subscribe<Position>();
        subscription.Dispose();

        world.GetComponent<Position>(a).X = 2f;
        world.AdvanceTick();

        var act = () => subscription.Drain();

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void MutationInTheTickImmediatelyAfterAScan_IsStillReported()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        using var subscription = world.Subscribe<Position>();

        world.GetComponent<Position>(a).X = 2f;
        world.AdvanceTick();
        subscription.Drain();

        world.GetComponent<Position>(a).X = 3f;
        world.AdvanceTick();

        var entries = subscription.Drain();

        entries.Should().ContainSingle();
        entries[0].Entity.Should().Be(a);
    }
}
