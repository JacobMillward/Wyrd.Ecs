namespace Wyrd.Ecs.Tests;

public class ChangeSubscriptionSharedScanTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    [Fact]
    public void TwoSubscribersOfTheSameType_ShareOneScanPerTick()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        using var first = world.Subscribe<Position>();
        using var second = world.Subscribe<Position>();

        world.GetComponent<Position>(a).X = 2f;
        world.AdvanceTick();

        world.DebugChangeFeedHub!.ScanCount.Should().Be(1);
        first.Drain().Should().ContainSingle();
        second.Drain().Should().ContainSingle();
    }

    [Fact]
    public void LastWatcherOfATypeLeaving_RetiresItsScanner_OtherTypesKeepScanning()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        Entity b = world.Commands.CreateEntity(new Velocity { X = 1f });
        world.ApplyCommands();

        var positionSubscription = world.Subscribe<Position>();
        using var velocitySubscription = world.Subscribe<Velocity>();

        positionSubscription.Dispose();

        // Two ticks: one scan per surviving type per tick. A retired scanner that kept
        // running would add one ghost scan per tick to the count.
        world.GetComponent<Velocity>(b).X = 2f;
        world.AdvanceTick();
        world.AdvanceTick();

        world.DebugChangeFeedHub!.ScanCount.Should().Be(2,
            "only Velocity remains watched: exactly one scan per tick after Position's last watcher left");
    }

    private struct Velocity : IComponent
    {
        public float X;
    }

    [Fact]
    public void TwoIndependentSubscriptions_EachGetTheirOwnBuffer()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        using var first = world.Subscribe<Position>();

        world.GetComponent<Position>(a).X = 2f;
        world.AdvanceTick();
        first.Drain();

        using var second = world.Subscribe<Position>();
        world.GetComponent<Position>(a).X = 3f;
        world.AdvanceTick();

        first.Drain().Should().ContainSingle();
        second.Drain().Should().ContainSingle();
    }

    [Fact]
    public void UnsubscribingTheOnlyWatcherOfAType_StopsTrackingIt_AndResubscribingResumesIt()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        var subscription = world.Subscribe<Position>();
        subscription.Dispose();

        using var replacement = world.Subscribe<Position>();
        world.GetComponent<Position>(a).X = 2f;
        world.AdvanceTick();

        world.DebugChangeFeedHub!.ScanCount.Should().Be(1);
        replacement.Drain().Should().ContainSingle();
    }
}
