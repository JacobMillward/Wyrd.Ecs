namespace Wyrd.Ecs.Tests;

file sealed class TestSystem : EcsSystem
{
    protected override void Execute(World world, Time time) { }
}

file sealed class TestMarker : MarkerSystem
{
}

public class SchedulableSystemTests
{
    [Fact]
    public void EcsSystem_IsASchedulableSystem()
    {
        var system = new TestSystem();

        (system is SchedulableSystem).Should().BeTrue();
    }

    [Fact]
    public void MarkerSystem_IsASchedulableSystem()
    {
        var marker = new TestMarker();

        (marker is SchedulableSystem).Should().BeTrue();
    }

    // MarkerSystem and EcsSystem are sibling types under SchedulableSystem, not related by
    // inheritance, so the compiler already proves "marker is EcsSystem" false (CS0184) - no
    // runtime assertion needed here.
}
