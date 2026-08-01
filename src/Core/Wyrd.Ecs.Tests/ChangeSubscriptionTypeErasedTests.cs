namespace Wyrd.Ecs.Tests;

public class ChangeSubscriptionTypeErasedTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private static ComponentCodecRegistry BuildRegistry()
    {
        var registry = new ComponentCodecRegistry();
        registry.Register<Position>("Position", v => BitConverter.GetBytes(v.X), d => new Position { X = BitConverter.ToSingle(d) });
        return registry;
    }

    [Fact]
    public void SubscribeWithACodec_ReportsAValueChangeAfterTheNextTickAdvance()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        var registry = BuildRegistry();
        registry.TryGetByTypeIndex(Wyrd.Ecs.Internal.TypeIndex<Position>.Value, out var codec);

        using var subscription = world.Subscribe(codec);

        world.GetComponent<Position>(a).X = 2f;
        world.AdvanceTick();

        var entries = subscription.Drain();

        entries.Should().ContainSingle();
        entries[0].Entity.Should().Be(a);
        entries[0].Kind.Should().Be(ChangeKind.ValueChanged);
        entries[0].Value.Should().BeOfType<Position>().Which.X.Should().Be(2f);
    }

    [Fact]
    public void GenericSubscribeAndTypeErasedSubscribe_ForTheSameType_ShareOneScanPerTick()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        var registry = BuildRegistry();
        registry.TryGetByTypeIndex(Wyrd.Ecs.Internal.TypeIndex<Position>.Value, out var codec);

        using var generic = world.Subscribe<Position>();
        using var erased = world.Subscribe(codec);

        world.GetComponent<Position>(a).X = 2f;
        world.AdvanceTick();

        world.DebugChangeFeedHub!.ScanCount.Should().Be(1);
        generic.Drain().Should().ContainSingle();
        erased.Drain().Should().ContainSingle();
    }

    [Fact]
    public void GenericSubscribe_AlsoPopulatesValue()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        using var subscription = world.Subscribe<Position>();

        world.GetComponent<Position>(a).X = 3f;
        world.AdvanceTick();

        var entries = subscription.Drain();

        entries[0].Value.Should().BeOfType<Position>().Which.X.Should().Be(3f);
    }

    [Fact]
    public void Dispose_StopsTypeErasedReporting()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        var registry = BuildRegistry();
        registry.TryGetByTypeIndex(Wyrd.Ecs.Internal.TypeIndex<Position>.Value, out var codec);

        var subscription = world.Subscribe(codec);
        subscription.Dispose();

        world.GetComponent<Position>(a).X = 2f;
        world.AdvanceTick();

        var act = () => subscription.Drain();

        act.Should().Throw<KeyNotFoundException>();
    }
}
