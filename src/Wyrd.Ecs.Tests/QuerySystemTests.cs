namespace Wyrd.Ecs.Tests;

internal struct Energy : IComponent
{
    public float Current;
    public float DrainPerSecond;
}

internal struct Marker : ITag;

internal sealed partial class EnergyDrainSystem : QuerySystem<Energy>
{
    protected override void Execute(World world, ulong tick, ref Energy component0)
    {
        component0.Current -= component0.DrainPerSecond;
    }
}

internal struct Transceiver : IComponent
{
    public float Bandwidth;
}

internal struct Outbox : IComponent
{
    public float SendProgress;
}

internal struct Inbox : IComponent
{
    public float ReceiveProgress;
}

internal sealed partial class ThreeComponentSystem : QuerySystem<Transceiver, Outbox, Inbox>
{
    protected override void Execute(World world, ulong tick, ref Transceiver transceiver, ref Outbox outbox, ref Inbox inbox)
    {
        outbox.SendProgress += transceiver.Bandwidth;
        inbox.ReceiveProgress += transceiver.Bandwidth;
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

        var system = new EnergyDrainSystem();
        system.RunOnce(world, tick: 1);

        world.GetComponent<Energy>(entity).Current.Should().Be(90f);
    }

    [Fact]
    public void QuerySystem_VisitsEveryMatchingEntityAcrossArchetypes()
    {
        var world = new World();
        var plain = world.CreateEntity();
        world.AddComponent<Energy>(plain) = new Energy { Current = 50f, DrainPerSecond = 5f };
        var tagged = world.CreateEntity();
        world.AddComponent<Energy>(tagged) = new Energy { Current = 20f, DrainPerSecond = 2f };
        world.AddTag<Marker>(tagged);

        var system = new EnergyDrainSystem();
        system.RunOnce(world, tick: 1);

        world.GetComponent<Energy>(plain).Current.Should().Be(45f);
        world.GetComponent<Energy>(tagged).Current.Should().Be(18f);
    }

    [Fact]
    public void QuerySystem_ThreeComponentArity_RunsAndWritesThroughAllThree()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Transceiver>(entity) = new Transceiver { Bandwidth = 3f };
        world.AddComponent<Outbox>(entity) = new Outbox { SendProgress = 0f };
        world.AddComponent<Inbox>(entity) = new Inbox { ReceiveProgress = 0f };
        var missingOne = world.CreateEntity();
        world.AddComponent<Transceiver>(missingOne);
        world.AddComponent<Outbox>(missingOne);

        var system = new ThreeComponentSystem();
        system.RunOnce(world, tick: 1);

        world.GetComponent<Outbox>(entity).SendProgress.Should().Be(3f);
        world.GetComponent<Inbox>(entity).ReceiveProgress.Should().Be(3f);
    }
}
