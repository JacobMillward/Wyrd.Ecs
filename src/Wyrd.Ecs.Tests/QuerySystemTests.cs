namespace Wyrd.Ecs.Tests;

internal struct Energy : IComponent
{
    public float Current;
    public float DrainPerSecond;
}

internal sealed class HandWrittenEnergyDrainSystem : QuerySystem<Energy>
{
    protected override void OnUpdate(World world, ulong tick)
    {
        foreach (var row in world.Query<Energy>())
            Execute(world, tick, ref row.Get<Energy>());
    }

    protected override void Execute(World world, ulong tick, ref Energy component0)
    {
        component0.Current -= component0.DrainPerSecond;
    }
}

public class QuerySystemTests
{
    [Fact]
    public void QuerySystem_ExecuteRunsPerMatchingEntity()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Energy>(entity) = new Energy { Current = 100f, DrainPerSecond = 10f };

        var system = new HandWrittenEnergyDrainSystem();
        system.RunOnce(world, tick: 1);

        world.GetComponent<Energy>(entity).Current.Should().Be(90f);
    }
}
