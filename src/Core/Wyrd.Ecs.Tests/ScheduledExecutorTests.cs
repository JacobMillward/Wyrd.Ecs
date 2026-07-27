namespace Wyrd.Ecs.Tests;

struct ScheduledPosition : IComponent { public float X; }
struct ScheduledHealth : IComponent { public int Value; }

sealed class MoveSystem : EcsSystem
{
    protected override void Execute(World world, Time time) =>
        world.Query().With<ScheduledPosition>().ForEach(0, (in int _, ref ScheduledPosition p) => p.X += 1f);
}

sealed class DamageSystem : EcsSystem
{
    protected override void Execute(World world, Time time) =>
        world.Query().With<ScheduledHealth>().ForEach(0, (in int _, ref ScheduledHealth h) => h.Value -= 1);
}

sealed class SpawnerSystem : EcsSystem
{
    protected override void Execute(World world, Time time)
    {
        var entity = world.Commands.CreateEntity();
        world.Commands.AddComponent(entity, new ScheduledHealth { Value = 10 });
    }
}

sealed class SpawnerASystem : EcsSystem
{
    protected override void Execute(World world, Time time)
    {
        for (var i = 0; i < 200; i++)
        {
            var entity = world.Commands.CreateEntity();
            world.Commands.AddComponent(entity, new ScheduledPosition { X = 1f });
        }
    }
}

sealed class SpawnerBSystem : EcsSystem
{
    protected override void Execute(World world, Time time)
    {
        for (var i = 0; i < 200; i++)
        {
            var entity = world.Commands.CreateEntity();
            world.Commands.AddComponent(entity, new ScheduledHealth { Value = 1 });
        }
    }
}

public class ScheduledExecutorTests
{
    [Fact]
    public void DisjointSystems_BothRun_EachStageInSequence()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(MoveSystem)] = new(Reads: [], Writes: [typeof(ScheduledPosition)]),
            [typeof(DamageSystem)] = new(Reads: [], Writes: [typeof(ScheduledHealth)]),
        };
        var world = new WorldBuilder().WithSystems(access, new MoveSystem(), new DamageSystem()).Build();
        var e = world.Commands.CreateEntity();
        world.Commands.AddComponent(e, new ScheduledPosition { X = 0f });
        world.Commands.AddComponent(e, new ScheduledHealth { Value = 5 });
        world.ApplyCommands();

        world.Tick(TimeSpan.Zero);

        world.GetComponent<ScheduledPosition>(e).X.Should().Be(1f); // MoveSystem ran exactly once
        world.GetComponent<ScheduledHealth>(e).Value.Should().Be(4); // DamageSystem ran exactly once
    }

    [Fact]
    public void StructuralChangesFromASystem_AreVisibleAfterRunTick()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(SpawnerSystem)] = new(Reads: [], Writes: [typeof(ScheduledHealth)]),
        };
        var world = new WorldBuilder().WithSystems(access, new SpawnerSystem()).Build();

        world.Tick(TimeSpan.Zero);

        var count = 0;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<ScheduledHealth>>().Resolve(world))
            count += chunk.Count;
        count.Should().Be(1); // SpawnerSystem's CreateEntity/AddComponent must be applied by RunTick, not left pending
    }

    [Fact]
    public void ConflictingSystems_BothStillRun_JustInSeparateStages()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(MoveSystem)] = new(Reads: [], Writes: [typeof(ScheduledPosition)]),
        };
        var world = new WorldBuilder().WithSystems(access, new MoveSystem(), new MoveSystem()).Build();
        var e = world.Commands.CreateEntity();
        world.Commands.AddComponent(e, new ScheduledPosition { X = 0f });
        world.ApplyCommands();

        world.Tick(TimeSpan.Zero);

        world.GetComponent<ScheduledPosition>(e).X.Should().Be(2f); // both MoveSystem instances ran, one per stage
    }

    [Fact]
    public void TwoSystemsInTheSameParallelStage_BothCreatingEntitiesConcurrently_AllEntitiesSurvive()
    {
        // WithParallelThreshold(0) forces RunTick's `stage.Count > 1 && world.TotalEntityCount
        // >= _parallelThreshold` check to take the Parallel.ForEach branch (both spawner
        // systems have disjoint writes, so the scheduler places them in one stage) -- unlike
        // this file's other tests, which never clear the default threshold and so only ever
        // exercise the sequential branch. Both systems hammer world.Commands concurrently, so
        // this is the one test that actually runs real user systems through ScheduledExecutor's
        // thread-pool dispatch against the shared CommandBuffer/EntityTable.
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(SpawnerASystem)] = new(Reads: [], Writes: [typeof(ScheduledPosition)]),
            [typeof(SpawnerBSystem)] = new(Reads: [], Writes: [typeof(ScheduledHealth)]),
        };
        var world = new WorldBuilder()
            .WithSystems(access, new SpawnerASystem(), new SpawnerBSystem())
            .WithParallelThreshold(0)
            .Build();

        world.Tick(TimeSpan.Zero);

        var positionCount = 0;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<ScheduledPosition>>().Resolve(world))
            positionCount += chunk.Count;
        var healthCount = 0;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<ScheduledHealth>>().Resolve(world))
            healthCount += chunk.Count;

        positionCount.Should().Be(200); // SpawnerASystem's 200 concurrent creates all survived
        healthCount.Should().Be(200); // SpawnerBSystem's 200 concurrent creates all survived, dispatched alongside SpawnerASystem
    }

    [Fact]
    public void Tick_AdvancesCurrentTickEachCall()
    {
        var access = new Dictionary<Type, SystemAccess>
        {
            [typeof(MoveSystem)] = new(Reads: [], Writes: [typeof(ScheduledPosition)]),
        };
        var world = new WorldBuilder().WithSystems(access, new MoveSystem()).Build();

        world.Tick(TimeSpan.FromSeconds(1));
        world.Tick(TimeSpan.FromSeconds(2));

        world.CurrentTick.Should().Be(3); // starts at 1 (World's own default), advanced once per Tick call
    }
}
