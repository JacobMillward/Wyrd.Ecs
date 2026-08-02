namespace Wyrd.Ecs.Tests;

sealed class DestroyRecordingSystem : EcsSystem
{
    public int OnDestroyCallCount;
    protected override void Execute(World world, Time time) { }
    protected override void OnDestroy() => OnDestroyCallCount++;
    public void InvokeDestroyForTest() => InvokeOnDestroy();
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
}
