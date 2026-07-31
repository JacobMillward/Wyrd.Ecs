namespace Wyrd.Ecs.Tests;

struct Energy : IComponent { public float Current; public float DrainPerSecond; }

sealed partial class DrainSystem : QuerySystem
{
    protected override IQuery DefineQuery(World world) => world.Query().With<Energy>();

    public void Update(Time time, ref Energy energy) => energy.Current -= energy.DrainPerSecond;
}

struct Attacked : IComponent { public bool Triggered; }
struct Poisoned : ITag { }
struct Spawned : IComponent { public int Value; }

sealed partial class SpawningSystem : QuerySystem
{
    protected override IQuery DefineQuery(World world) => world.Query().With<Energy>();

    public void Update(Time time, World world, ref Energy energy)
    {
        if (energy.Current <= 0f)
            world.Commands.CreateEntity(new Spawned { Value = 1 });
    }
}

sealed partial class PoisonSystem : QuerySystem
{
    protected override IQuery DefineQuery(World world) => world.Query().With<Attacked>();

    public void Update(Time time, EntityView entity, ref Attacked a)
    {
        if (a.Triggered)
            entity.AddTag<Poisoned>();
    }
}

sealed partial class SpawnAndTagSystem : QuerySystem
{
    protected override IQuery DefineQuery(World world) => world.Query().With<Attacked>();

    public void Update(Time time, World world, EntityView entity, ref Attacked a)
    {
        if (a.Triggered)
        {
            entity.AddTag<Poisoned>();
            world.Commands.CreateEntity(new Spawned { Value = 1 });
        }
    }
}

public class QuerySystemTests
{
    [Fact]
    public void DeclaredSystem_RunOnce_MutatesThroughToRealStorage()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new Energy { Current = 100f, DrainPerSecond = 10f });
        world.ApplyCommands();

        world.RunOnce(new DrainSystem(), TimeSpan.Zero);

        world.GetComponent<Energy>(entity).Current.Should().Be(90f);
    }

    [Fact]
    public void DeclaredSystem_RunOnce_VisitsEveryMatchingEntity()
    {
        var world = new World();
        var entities = new Entity[5];
        for (var i = 0; i < entities.Length; i++)
            entities[i] = world.Commands.CreateEntity(new Energy { Current = 100f, DrainPerSecond = 1f });
        world.ApplyCommands();

        world.RunOnce(new DrainSystem(), TimeSpan.Zero);

        foreach (var entity in entities)
            world.GetComponent<Energy>(entity).Current.Should().Be(99f);
    }

    [Fact]
    public void DeclaredSystem_RegistersGeneratedSystemAccess()
    {
        Wyrd.Ecs.Generated.GeneratedSystemAccess.Entries.Should().ContainKey(typeof(DrainSystem));
        var access = Wyrd.Ecs.Generated.GeneratedSystemAccess.Entries[typeof(DrainSystem)];
        access.Writes.Should().Equal(typeof(Energy));
        access.Reads.Should().BeEmpty();
    }

    [Fact]
    public void DeclaredSystem_UpdateWithWorldParameter_CanCreateNewEntities()
    {
        var world = new World();
        world.Commands.CreateEntity(new Energy { Current = 0f, DrainPerSecond = 0f });
        world.ApplyCommands();

        world.RunOnce(new SpawningSystem(), TimeSpan.Zero);
        world.ApplyCommands();

        var spawnedCount = 0;
        world.Query().With<Spawned>().ForEach((in Spawned _) => spawnedCount++);
        spawnedCount.Should().Be(1);
    }

    [Fact]
    public void DeclaredSystem_UpdateWithEntityViewParameter_CanTagItsOwnRow()
    {
        var world = new World();
        Entity triggered = world.Commands.CreateEntity(new Attacked { Triggered = true });
        Entity untouched = world.Commands.CreateEntity(new Attacked { Triggered = false });
        world.ApplyCommands();

        world.RunOnce(new PoisonSystem(), TimeSpan.Zero);
        world.ApplyCommands();

        world.HasTag<Poisoned>(triggered).Should().BeTrue();
        world.HasTag<Poisoned>(untouched).Should().BeFalse();
    }

    [Fact]
    public void DeclaredSystem_UpdateWithWorldAndEntityViewParameters_CanUseBoth()
    {
        var world = new World();
        Entity triggered = world.Commands.CreateEntity(new Attacked { Triggered = true });
        world.ApplyCommands();

        world.RunOnce(new SpawnAndTagSystem(), TimeSpan.Zero);
        world.ApplyCommands();

        world.HasTag<Poisoned>(triggered).Should().BeTrue();
        var spawnedCount = 0;
        world.Query().With<Spawned>().ForEach((in Spawned _) => spawnedCount++);
        spawnedCount.Should().Be(1);
    }
}
