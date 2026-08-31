namespace Wyrd.Ecs.Tests;

file sealed class ExitAfterNTicksSystem(int ticksBeforeExit) : EcsSystem
{
    public int ExecuteCount { get; private set; }

    protected override void Execute(World world, Time time)
    {
        ExecuteCount++;
        if (ExecuteCount >= ticksBeforeExit) world.RequestExit();
    }
}

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

    [Fact]
    public void Run_ReturnsOnceASystemCallsRequestExit()
    {
        var builder = new WorldBuilder();
        builder.AddSystemCore(typeof(ExitAfterNTicksSystem), access: null, _ => new ExitAfterNTicksSystem(3), [], []);
        var world = builder.Build();

        var act = () => world.Run();

        act.Should().NotThrow();
        world.GetSystem<ExitAfterNTicksSystem>().ExecuteCount.Should().Be(3);
    }

    [Fact]
    public void Run_WithTargetFrameTime_StillReturnsOnceASystemCallsRequestExit()
    {
        var builder = new WorldBuilder();
        builder.AddSystemCore(typeof(ExitAfterNTicksSystem), access: null, _ => new ExitAfterNTicksSystem(2), [], []);
        var world = builder.Build();

        var act = () => world.Run(TimeSpan.FromMilliseconds(1));

        act.Should().NotThrow();
        world.GetSystem<ExitAfterNTicksSystem>().ExecuteCount.Should().Be(2);
    }
}
