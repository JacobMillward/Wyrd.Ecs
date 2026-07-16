namespace Wyrd.Ecs.Tests;

public class WorldTickTests
{
    [Fact]
    public void NewWorld_CurrentTickStartsAtOne()
    {
        var world = new World();

        world.CurrentTick.Should().Be(1);
    }

    [Fact]
    public void AdvanceTick_IncrementsCurrentTickByOne()
    {
        var world = new World();

        world.AdvanceTick();

        world.CurrentTick.Should().Be(2);
    }

    [Fact]
    public void AdvanceTick_CalledRepeatedly_KeepsIncrementing()
    {
        var world = new World();

        world.AdvanceTick();
        world.AdvanceTick();
        world.AdvanceTick();

        world.CurrentTick.Should().Be(4);
    }
}
