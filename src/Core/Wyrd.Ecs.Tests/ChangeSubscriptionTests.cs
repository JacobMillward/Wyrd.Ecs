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
    /// Ticks are coarse (every mutation within tick N shares timestamp N), so the scan
    /// watermark at subscribe time must be set to <c>CurrentTick - 1</c>, not
    /// <c>CurrentTick</c> — otherwise a mutation made later in the same tick Subscribe
    /// was called in would be silently missed by the first scan. This is the same root
    /// cause <c>ChangeCapture</c>'s own <c>_sinceTick = CurrentTick - 1</c> fix
    /// addresses (Plan 4d of the continuous-persistence arc).
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
}
