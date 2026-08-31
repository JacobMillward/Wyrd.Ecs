namespace Wyrd.Ecs.Tests;

public class WorldRunTests
{
    [Fact]
    public void RequestExit_EmitsAnExitEventWithTheGivenCode()
    {
        var world = new World();
        var reader = world.CreateEventReader<Exit>();

        world.RequestExit(42);

        reader.Read().Should().ContainSingle(e => e.Code == 42);
    }

    [Fact]
    public void RequestExit_DefaultsCodeToZero()
    {
        var world = new World();
        var reader = world.CreateEventReader<Exit>();

        world.RequestExit();

        reader.Read().Should().ContainSingle(e => e.Code == 0);
    }
}
