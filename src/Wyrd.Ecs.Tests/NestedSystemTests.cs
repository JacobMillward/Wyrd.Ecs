namespace Wyrd.Ecs.Tests;

public partial class NestedSystemTests
{
    private struct Energy : IComponent
    {
        public float Current;
        public float DrainPerSecond;
    }

    private sealed partial class NestedEnergyDrainSystem : QuerySystem<Energy>
    {
        protected override void Execute(World world, ulong tick, ref Energy component0)
        {
            component0.Current -= component0.DrainPerSecond;
        }
    }

    [Fact]
    public void QuerySystem_NestedInsideAnotherClass_StillWorks()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Energy>(entity) = new Energy { Current = 100f, DrainPerSecond = 10f };

        var system = new NestedEnergyDrainSystem();
        system.RunOnce(world, tick: 1);

        world.GetComponent<Energy>(entity).Current.Should().Be(90f);
    }
}
