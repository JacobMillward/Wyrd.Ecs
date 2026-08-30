namespace Wyrd.Ecs.Tests;

sealed class DestroyRecordingSystem : EcsSystem
{
    public int OnDestroyCallCount;
    protected override void Execute(World world, Time time) { }
    protected override void OnDestroy() => OnDestroyCallCount++;
    public void InvokeDestroyForTest() => InvokeOnDestroy();
}

sealed class CurrentWorldProbeSystem : EcsSystem
{
    public World? ObservedWorld { get; private set; }
    protected override void Execute(World world, Time time) => ObservedWorld = CurrentWorld;
}

public class EcsSystemTests
{
    [Fact]
    public void InvokeOnDestroy_CallsOnDestroyExactlyOnce()
    {
        var system = new DestroyRecordingSystem();

        system.InvokeDestroyForTest();

        system.OnDestroyCallCount.Should().Be(1);
    }

    [Fact]
    public void CurrentWorld_ReflectsTheWorldPassedToExecute()
    {
        var world = new World();
        var system = new CurrentWorldProbeSystem();

        system.InvokeExecute(world, default);

        system.ObservedWorld.Should().BeSameAs(world);
    }
}
