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

    // MarkerSystem and EcsSystem are declared as siblings under SchedulableSystem
    // (Task 1), not related by inheritance -- the compiler already proves
    // "marker is EcsSystem" false at compile time (CS0184 on any attempt to write
    // that check at runtime), so there's no separate runtime assertion to make here.
}
