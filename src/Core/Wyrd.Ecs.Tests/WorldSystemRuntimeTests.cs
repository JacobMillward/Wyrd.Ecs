namespace Wyrd.Ecs.Tests;

sealed class LoggingSystemA : EcsSystem
{
    private readonly List<string> _log;
    public LoggingSystemA(List<string> log) => _log = log;
    protected override void Execute(World world, Time time) => _log.Add("A");
}

[RunAfter(typeof(LoggingSystemA))]
sealed class LoggingSystemB : EcsSystem
{
    private readonly List<string> _log;
    public LoggingSystemB(List<string> log) => _log = log;
    protected override void Execute(World world, Time time) => _log.Add("B");
}

public class WorldSystemRuntimeTests
{
    [Fact]
    public void AddSystem_ThenUpdate_RunsTheNewSystem()
    {
        var world = new WorldBuilder().Build();
        world.AddSystem<RecordingSystem>();

        world.Update(TimeSpan.FromSeconds(1));

        world.GetSystem<RecordingSystem>().ExecuteCallCount.Should().Be(1);
    }

    [Fact]
    public void RemoveSystem_CallsOnDestroyExactlyOnce_AndStopsFutureExecution()
    {
        var world = new WorldBuilder().Build();
        world.AddSystem<DestroyRecordingSystem>();
        world.Update(TimeSpan.FromSeconds(1));

        var system = world.GetSystem<DestroyRecordingSystem>(); // capture before removal — GetSystem throws once it's gone
        var removed = world.RemoveSystem<DestroyRecordingSystem>();
        world.Update(TimeSpan.FromSeconds(1));

        removed.Should().BeTrue();
        system.OnDestroyCallCount.Should().Be(1, "OnDestroy fires exactly once, on removal");
        world.TryGetSystem<DestroyRecordingSystem>(out _).Should().BeFalse("Find no longer reports it as registered after removal");
    }

    [Fact]
    public void GetSystem_ThrowsWhenNotRegistered()
    {
        var world = new WorldBuilder().Build();
        var act = () => world.GetSystem<RecordingSystem>();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryGetSystem_ReturnsFalseWhenNotRegistered()
    {
        var world = new WorldBuilder().Build();
        world.TryGetSystem<RecordingSystem>(out var system).Should().BeFalse();
        system.Should().BeNull();
    }

    [Fact]
    public void AddSystem_Before_OrdersCorrectlyEvenWhenTargetRegisteredLater()
    {
        var log = new List<string>();
        var world = new WorldBuilder().Build();

        // B is registered first, declaring [RunAfter(typeof(A))] — A doesn't exist yet at
        // this point. The next Update()'s deferred recompute sees the full live set (both
        // A and B) and places them correctly regardless of registration order.
        world.AddSystem<LoggingSystemB>(w => new LoggingSystemB(log));
        world.AddSystem<LoggingSystemA>(w => new LoggingSystemA(log));

        world.Update(TimeSpan.FromSeconds(1));

        log.Should().Equal("A", "B");
    }
}
