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
        Entity entity = world.Commands.CreateEntity();
        world.Commands.AddComponent(entity, new ScheduledHealth { Value = 10 });
    }
}

sealed class SpawnerASystem : EcsSystem
{
    protected override void Execute(World world, Time time)
    {
        for (var i = 0; i < 200; i++)
        {
            Entity entity = world.Commands.CreateEntity();
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
            Entity entity = world.Commands.CreateEntity();
            world.Commands.AddComponent(entity, new ScheduledHealth { Value = 1 });
        }
    }
}

sealed class RecordingSystem : EcsSystem
{
    public int ExecuteCallCount;
    protected override void Execute(World world, Time time) => ExecuteCallCount++;
}

public class ParallelSystemSchedulerTests
{
    [Fact]
    public void DisjointSystems_BothRun_EachStageInSequence()
    {
        var builder = new WorldBuilder();
        builder.AddSystemCore(typeof(MoveSystem), new(Reads: [], Writes: [typeof(ScheduledPosition)]), _ => new MoveSystem(), [], []);
        builder.AddSystemCore(typeof(DamageSystem), new(Reads: [], Writes: [typeof(ScheduledHealth)]), _ => new DamageSystem(), [], []);
        var world = builder.Build();

        Entity e = world.Commands.CreateEntity();
        world.Commands.AddComponent(e, new ScheduledPosition { X = 0f });
        world.Commands.AddComponent(e, new ScheduledHealth { Value = 5 });
        world.ApplyCommands();

        world.Update(TimeSpan.Zero);

        world.GetComponent<ScheduledPosition>(e).X.Should().Be(1f, "MoveSystem ran exactly once");
        world.GetComponent<ScheduledHealth>(e).Value.Should().Be(4, "DamageSystem ran exactly once");
    }

    [Fact]
    public void StructuralChangesFromASystem_AreVisibleAfterUpdate()
    {
        var builder = new WorldBuilder();
        builder.AddSystemCore(typeof(SpawnerSystem), new(Reads: [], Writes: [typeof(ScheduledHealth)]), _ => new SpawnerSystem(), [], []);
        var world = builder.Build();

        world.Update(TimeSpan.Zero);

        var count = 0;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<ScheduledHealth>>().Resolve(world))
            count += chunk.Count;
        count.Should().Be(1, "SpawnerSystem's CreateEntity/AddComponent must be applied by RunStages, not left pending");
    }

    [Fact]
    public void ConflictingSystems_BothStillRun_JustInSeparateStages()
    {
        var builder = new WorldBuilder();
        builder.AddSystemCore(typeof(MoveSystem), new(Reads: [], Writes: [typeof(ScheduledPosition)]), _ => new MoveSystem(), [], []);
        builder.AddSystemCore(typeof(MoveSystem), new(Reads: [], Writes: [typeof(ScheduledPosition)]), _ => new MoveSystem(), [], []);
        var world = builder.Build();

        Entity e = world.Commands.CreateEntity();
        world.Commands.AddComponent(e, new ScheduledPosition { X = 0f });
        world.ApplyCommands();

        world.Update(TimeSpan.Zero);

        world.GetComponent<ScheduledPosition>(e).X.Should().Be(2f, "both MoveSystem instances ran, one per stage");
    }

    [Fact]
    public void TwoSystemsInTheSameParallelStage_BothCreatingEntitiesConcurrently_AllEntitiesSurvive()
    {
        // WithParallelThreshold(0) forces the Parallel.ForEach branch (disjoint writes put both
        // spawner systems in one stage); this is the only test in the file that exercises real
        // concurrent dispatch through ParallelSystemScheduler's thread pool.
        var builder = new WorldBuilder();
        builder.AddSystemCore(typeof(SpawnerASystem), new(Reads: [], Writes: [typeof(ScheduledPosition)]), _ => new SpawnerASystem(), [], []);
        builder.AddSystemCore(typeof(SpawnerBSystem), new(Reads: [], Writes: [typeof(ScheduledHealth)]), _ => new SpawnerBSystem(), [], []);
        var world = builder.WithParallelThreshold(0).Build();

        world.Update(TimeSpan.Zero);

        var positionCount = 0;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<ScheduledPosition>>().Resolve(world))
            positionCount += chunk.Count;
        var healthCount = 0;
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<ScheduledHealth>>().Resolve(world))
            healthCount += chunk.Count;

        positionCount.Should().Be(200, "SpawnerASystem's 200 concurrent creates all survived");
        healthCount.Should().Be(200, "SpawnerBSystem's 200 concurrent creates all survived, dispatched alongside SpawnerASystem");
    }

    [Fact]
    public void Update_AdvancesCurrentTickEachCall()
    {
        var builder = new WorldBuilder();
        builder.AddSystemCore(typeof(MoveSystem), new(Reads: [], Writes: [typeof(ScheduledPosition)]), _ => new MoveSystem(), [], []);
        var world = builder.Build();

        world.Update(TimeSpan.FromSeconds(1));
        world.Update(TimeSpan.FromSeconds(2));

        world.CurrentTick.Should().Be(3, "it starts at 1 (World's own default), advanced once per Update call");
    }

    [Fact]
    public void RunStages_SkipsExecuteForDisabledSystem()
    {
        var system = new RecordingSystem();
        var stages = new List<IReadOnlyList<EcsSystem>> { new List<EcsSystem> { system } };
        var scheduler = new ParallelSystemScheduler(parallelThreshold: 1000);
        scheduler.AttachStages(stages);
        var world = new World();

        system.Enabled = false;
        scheduler.RunStages(world, new Time(TimeSpan.Zero, TimeSpan.Zero));

        system.ExecuteCallCount.Should().Be(0);
    }

    [Fact]
    public void RunStages_RunsExecuteForEnabledSystem()
    {
        var system = new RecordingSystem();
        var stages = new List<IReadOnlyList<EcsSystem>> { new List<EcsSystem> { system } };
        var scheduler = new ParallelSystemScheduler(parallelThreshold: 1000);
        scheduler.AttachStages(stages);
        var world = new World();

        scheduler.RunStages(world, new Time(TimeSpan.Zero, TimeSpan.Zero));

        system.ExecuteCallCount.Should().Be(1, "Enabled defaults to true");
    }
}
