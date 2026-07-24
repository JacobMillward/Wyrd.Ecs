namespace Wyrd.Ecs.Tests;

public class WorldTickAdvancedTests
{
    [Fact]
    public void AdvanceTick_RaisesOnTickAdvanced_WithTheNewTickValue()
    {
        var world = new World();
        var observed = new List<int>();
        world.OnTickAdvanced += tick => observed.Add(tick);

        world.AdvanceTick();
        world.AdvanceTick();

        observed.Should().Equal(2, 3);
    }

    [Fact]
    public void AdvanceTick_WithNoSubscribers_DoesNotThrow()
    {
        var world = new World();

        var act = () => world.AdvanceTick();

        act.Should().NotThrow();
    }

    [Fact]
    public void Unsubscribing_StopsFurtherNotifications()
    {
        var world = new World();
        var observed = new List<int>();
        void Handler(int tick) => observed.Add(tick);
        world.OnTickAdvanced += Handler;

        world.AdvanceTick();
        world.OnTickAdvanced -= Handler;
        world.AdvanceTick();

        observed.Should().Equal(2);
    }
}
