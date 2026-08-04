namespace Wyrd.Ecs.Tests;

struct ScheduledPosition : IComponent { public float X; }
struct ScheduledHealth : IComponent { public int Value; }

sealed class MoveSystem : EcsSystem
{
    protected override void Execute(World world, Time time) =>
        world.Query().With<ScheduledPosition>().ForEach(0, (in int _, ref ScheduledPosition p) => p.X += 1f);
}

/// <summary>A second, distinct type also writing ScheduledPosition — used to keep testing "two conflicting systems still both run, in separate stages" now that registering the same Type twice is rejected.</summary>
sealed class MoveSystemDuplicateWriter : EcsSystem
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

sealed class RecordingSystemA : EcsSystem { public int ExecuteCallCount; protected override void Execute(World world, Time time) => ExecuteCallCount++; }
sealed class RecordingSystemB : EcsSystem { public int ExecuteCallCount; protected override void Execute(World world, Time time) => ExecuteCallCount++; }
sealed class RecordingSystemC : EcsSystem { public int ExecuteCallCount; protected override void Execute(World world, Time time) => ExecuteCallCount++; }
sealed class RecordingSystemD : EcsSystem { public int ExecuteCallCount; protected override void Execute(World world, Time time) => ExecuteCallCount++; }
sealed class RecordingSystemE : EcsSystem { public int ExecuteCallCount; protected override void Execute(World world, Time time) => ExecuteCallCount++; }

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
        builder.AddSystemCore(typeof(MoveSystemDuplicateWriter), new(Reads: [], Writes: [typeof(ScheduledPosition)]), _ => new MoveSystemDuplicateWriter(), [], []);
        var world = builder.Build();

        Entity e = world.Commands.CreateEntity();
        world.Commands.AddComponent(e, new ScheduledPosition { X = 0f });
        world.ApplyCommands();

        world.Update(TimeSpan.Zero);

        world.GetComponent<ScheduledPosition>(e).X.Should().Be(2f, "both systems ran, one per stage, despite writing the same component");
    }

    [Fact]
    public void DuplicateSystemType_ThrowsAtRegistration()
    {
        var builder = new WorldBuilder();
        builder.AddSystemCore(typeof(MoveSystem), new(Reads: [], Writes: [typeof(ScheduledPosition)]), _ => new MoveSystem(), [], []);

        var act = () => builder.AddSystemCore(typeof(MoveSystem), new(Reads: [], Writes: [typeof(ScheduledPosition)]), _ => new MoveSystem(), [], []);

        act.Should().Throw<InvalidOperationException>().WithMessage("*MoveSystem*already registered*");
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
        RecordingSystem? system = null;
        var scheduler = new ParallelSystemScheduler(parallelThreshold: 1000);
        var world = new World(World.DefaultArchetypeCapacity, scheduler);
        var entry = new SystemEntry { SystemType = typeof(RecordingSystem), Construct = _ => system = new RecordingSystem(), Access = null };
        scheduler.InitialRegister([entry], world);

        system!.Enabled = false;
        scheduler.RunStages(world, new Time(TimeSpan.Zero, TimeSpan.Zero), SystemCadence.Variable);

        system.ExecuteCallCount.Should().Be(0);
    }

    [Fact]
    public void RunStages_RunsExecuteForEnabledSystem()
    {
        RecordingSystem? system = null;
        var scheduler = new ParallelSystemScheduler(parallelThreshold: 1000);
        var world = new World(World.DefaultArchetypeCapacity, scheduler);
        var entry = new SystemEntry { SystemType = typeof(RecordingSystem), Construct = _ => system = new RecordingSystem(), Access = null };
        scheduler.InitialRegister([entry], world);

        scheduler.RunStages(world, new Time(TimeSpan.Zero, TimeSpan.Zero), SystemCadence.Variable);

        system!.ExecuteCallCount.Should().Be(1, "Enabled defaults to true");
    }

    [Fact]
    public void Register_MarksDirty_RecomputeHappensOnNextRunStagesNotImmediately()
    {
        var scheduler = new ParallelSystemScheduler(parallelThreshold: 1000);
        var world = new World(World.DefaultArchetypeCapacity, scheduler);
        var entry = new SystemEntry { SystemType = typeof(RecordingSystemA), Construct = _ => new RecordingSystemA(), Access = null };

        scheduler.Register(entry, world);
        // No RunStages call yet: Find still reflects the instance immediately, independent
        // of whether a recompute has happened.
        scheduler.Find(typeof(RecordingSystemA)).Should().BeSameAs(entry.Instance);

        scheduler.RunStages(world, new Time(TimeSpan.Zero, TimeSpan.Zero), SystemCadence.Variable);
        ((RecordingSystemA)entry.Instance!).ExecuteCallCount.Should().Be(1);
    }

    [Fact]
    public void Remove_TakesEffectByTheNextRunStages()
    {
        var scheduler = new ParallelSystemScheduler(parallelThreshold: 1000);
        var world = new World(World.DefaultArchetypeCapacity, scheduler);
        var entry = new SystemEntry { SystemType = typeof(RecordingSystemB), Construct = _ => new RecordingSystemB(), Access = null };
        scheduler.Register(entry, world);
        scheduler.RunStages(world, new Time(TimeSpan.Zero, TimeSpan.Zero), SystemCadence.Variable);

        scheduler.Remove(entry.Instance!).Should().BeTrue();
        scheduler.RunStages(world, new Time(TimeSpan.Zero, TimeSpan.Zero), SystemCadence.Variable);

        ((RecordingSystemB)entry.Instance!).ExecuteCallCount.Should().Be(1, "not incremented again after removal");
    }

    [Fact]
    public void MultipleMutationsBetweenRunStagesCallsCoalesceIntoOneRecompute()
    {
        var scheduler = new ParallelSystemScheduler(parallelThreshold: 1000);
        var world = new World(World.DefaultArchetypeCapacity, scheduler);
        SystemEntry EntryFor(Type type, Func<World, EcsSystem> construct) => new() { SystemType = type, Construct = construct, Access = null };

        var entries = new[]
        {
            EntryFor(typeof(RecordingSystemA), _ => new RecordingSystemA()),
            EntryFor(typeof(RecordingSystemB), _ => new RecordingSystemB()),
            EntryFor(typeof(RecordingSystemC), _ => new RecordingSystemC()),
            EntryFor(typeof(RecordingSystemD), _ => new RecordingSystemD()),
            EntryFor(typeof(RecordingSystemE), _ => new RecordingSystemE()),
        };
        foreach (var entry in entries) scheduler.Register(entry, world);

        // Five Register calls happened since the last (nonexistent) RunStages call; only
        // one recompute should be needed to place all five correctly before this call runs
        // them.
        scheduler.RunStages(world, new Time(TimeSpan.Zero, TimeSpan.Zero), SystemCadence.Variable);

        ((RecordingSystemA)entries[0].Instance!).ExecuteCallCount.Should().Be(1);
        ((RecordingSystemB)entries[1].Instance!).ExecuteCallCount.Should().Be(1);
        ((RecordingSystemC)entries[2].Instance!).ExecuteCallCount.Should().Be(1);
        ((RecordingSystemD)entries[3].Instance!).ExecuteCallCount.Should().Be(1);
        ((RecordingSystemE)entries[4].Instance!).ExecuteCallCount.Should().Be(1);
    }
}
