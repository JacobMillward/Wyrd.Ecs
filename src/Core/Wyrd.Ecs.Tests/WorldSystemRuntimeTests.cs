namespace Wyrd.Ecs.Tests;

sealed class LoggingSystemA : EcsSystem
{
    private readonly List<string> _log;
    public LoggingSystemA(List<string> log) => _log = log;
    protected override void Execute(World world, Time time) => _log.Add("A");
}

[RunAfter(typeof(LoggingSystemA))]
sealed class LoggingSystemB : EcsSystem
{
    private readonly List<string> _log;
    public LoggingSystemB(List<string> log) => _log = log;
    protected override void Execute(World world, Time time) => _log.Add("B");
}

file struct Slot0; file struct Slot1; file struct Slot2; file struct Slot3; file struct Slot4;
file struct Slot5; file struct Slot6; file struct Slot7; file struct Slot8; file struct Slot9;

file sealed class SpawnedSystem<TSlot> : EcsSystem
{
    protected override void Execute(World world, Time time) { }
}

/// <summary>
/// Registers a distinct <see cref="SpawnedSystem{TSlot}"/> from within its own Execute.
/// Every instance of this generic (one per distinct TSlot) declares a disjoint access
/// footprint (Writes: [typeof(TSlot)]), so with WithParallelThreshold(0) they all pack
/// into one stage and run concurrently via Parallel.ForEach - the exact scenario where
/// ParallelSystemScheduler's registration state needs to be safe against concurrent
/// AddSystemCore calls from multiple threads at once.
/// </summary>
file sealed class TriggerSystem<TSlot> : EcsSystem
{
    protected override void Execute(World world, Time time)
    {
        Thread.SpinWait(2000); // widen the race window so concurrent Executes are more likely to actually overlap inside AddSystemCore
        world.AddSystemCore(typeof(SpawnedSystem<TSlot>), null, _ => new SpawnedSystem<TSlot>(), [], []);
    }
}

public class WorldSystemRuntimeTests
{
    [Fact]
    public void AddSystem_ThenUpdate_RunsTheNewSystem()
    {
        var world = new WorldBuilder().Build();
        world.AddSystem<RecordingSystem>();

        world.Update(TimeSpan.FromSeconds(1));

        world.GetSystem<RecordingSystem>().ExecuteCallCount.Should().Be(1);
    }

    [Fact]
    public void RemoveSystem_CallsOnDestroyExactlyOnce_AndStopsFutureExecution()
    {
        var world = new WorldBuilder().Build();
        world.AddSystem<DestroyRecordingSystem>();
        world.Update(TimeSpan.FromSeconds(1));

        var system = world.GetSystem<DestroyRecordingSystem>(); // capture before removal - GetSystem throws once it's gone
        var removed = world.RemoveSystem<DestroyRecordingSystem>();
        world.Update(TimeSpan.FromSeconds(1));

        removed.Should().BeTrue();
        system.OnDestroyCallCount.Should().Be(1, "OnDestroy fires exactly once, on removal");
        world.TryGetSystem<DestroyRecordingSystem>(out _).Should().BeFalse("Find no longer reports it as registered after removal");
    }

    [Fact]
    public void GetSystem_ThrowsWhenNotRegistered()
    {
        var world = new WorldBuilder().Build();
        var act = () => world.GetSystem<RecordingSystem>();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TryGetSystem_ReturnsFalseWhenNotRegistered()
    {
        var world = new WorldBuilder().Build();
        world.TryGetSystem<RecordingSystem>(out var system).Should().BeFalse();
        system.Should().BeNull();
    }

    [Fact]
    public void AddSystem_Before_OrdersCorrectlyEvenWhenTargetRegisteredLater()
    {
        var log = new List<string>();
        var world = new WorldBuilder().Build();

        // B is registered first, declaring [RunAfter(typeof(A))] - A doesn't exist yet at
        // this point. The next Update()'s deferred recompute sees the full live set (both
        // A and B) and places them correctly regardless of registration order.
        world.AddSystem<LoggingSystemB>(w => new LoggingSystemB(log));
        world.AddSystem<LoggingSystemA>(w => new LoggingSystemA(log));

        world.Update(TimeSpan.FromSeconds(1));

        log.Should().Equal("A", "B");
    }

    [Fact]
    public void AddSystem_DuplicateType_ThrowsAtRegistration()
    {
        var world = new WorldBuilder().Build();
        world.AddSystem<RecordingSystem>();

        var act = () => world.AddSystem<RecordingSystem>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*RecordingSystem*already registered*");
    }

    [Fact]
    public void FlushSystemChanges_SurfacesAValidationErrorImmediately_NotOnlyOnNextUpdate()
    {
        var world = new WorldBuilder().Build();
        // After<RecordingSystem>() targets a type that is never registered - the call
        // itself succeeds (only marks the schedule dirty); FlushSystemChanges forces the
        // recompute (and its validation) to happen right here instead of on the next
        // Update().
        world.AddSystem<DestroyRecordingSystem>().After<RecordingSystem>();

        var act = () => world.FlushSystemChanges();

        act.Should().Throw<InvalidOperationException>().WithMessage("*RecordingSystem*");
    }

    [Fact]
    public void FlushSystemChanges_WhenNothingIsDirty_DoesNotThrowOrRecompute()
    {
        var world = new WorldBuilder().Build();
        world.AddSystem<RecordingSystem>();
        world.Update(TimeSpan.FromSeconds(1)); // clears dirty via the normal path

        var act = () => world.FlushSystemChanges();

        act.Should().NotThrow();
    }

    [Fact]
    public void ConcurrentAddSystemCoreCallsFromParallelExecute_DoNotCorruptRegistrationState()
    {
        var builder = new WorldBuilder().WithParallelThreshold(0);
        builder.AddSystemCore(typeof(TriggerSystem<Slot0>), new(Reads: [], Writes: [typeof(Slot0)]), _ => new TriggerSystem<Slot0>(), [], []);
        builder.AddSystemCore(typeof(TriggerSystem<Slot1>), new(Reads: [], Writes: [typeof(Slot1)]), _ => new TriggerSystem<Slot1>(), [], []);
        builder.AddSystemCore(typeof(TriggerSystem<Slot2>), new(Reads: [], Writes: [typeof(Slot2)]), _ => new TriggerSystem<Slot2>(), [], []);
        builder.AddSystemCore(typeof(TriggerSystem<Slot3>), new(Reads: [], Writes: [typeof(Slot3)]), _ => new TriggerSystem<Slot3>(), [], []);
        builder.AddSystemCore(typeof(TriggerSystem<Slot4>), new(Reads: [], Writes: [typeof(Slot4)]), _ => new TriggerSystem<Slot4>(), [], []);
        builder.AddSystemCore(typeof(TriggerSystem<Slot5>), new(Reads: [], Writes: [typeof(Slot5)]), _ => new TriggerSystem<Slot5>(), [], []);
        builder.AddSystemCore(typeof(TriggerSystem<Slot6>), new(Reads: [], Writes: [typeof(Slot6)]), _ => new TriggerSystem<Slot6>(), [], []);
        builder.AddSystemCore(typeof(TriggerSystem<Slot7>), new(Reads: [], Writes: [typeof(Slot7)]), _ => new TriggerSystem<Slot7>(), [], []);
        builder.AddSystemCore(typeof(TriggerSystem<Slot8>), new(Reads: [], Writes: [typeof(Slot8)]), _ => new TriggerSystem<Slot8>(), [], []);
        builder.AddSystemCore(typeof(TriggerSystem<Slot9>), new(Reads: [], Writes: [typeof(Slot9)]), _ => new TriggerSystem<Slot9>(), [], []);
        var world = builder.Build();

        var act = () => world.Update(TimeSpan.FromSeconds(1));

        act.Should().NotThrow();
        world.TryGetSystem<SpawnedSystem<Slot0>>(out _).Should().BeTrue();
        world.TryGetSystem<SpawnedSystem<Slot1>>(out _).Should().BeTrue();
        world.TryGetSystem<SpawnedSystem<Slot2>>(out _).Should().BeTrue();
        world.TryGetSystem<SpawnedSystem<Slot3>>(out _).Should().BeTrue();
        world.TryGetSystem<SpawnedSystem<Slot4>>(out _).Should().BeTrue();
        world.TryGetSystem<SpawnedSystem<Slot5>>(out _).Should().BeTrue();
        world.TryGetSystem<SpawnedSystem<Slot6>>(out _).Should().BeTrue();
        world.TryGetSystem<SpawnedSystem<Slot7>>(out _).Should().BeTrue();
        world.TryGetSystem<SpawnedSystem<Slot8>>(out _).Should().BeTrue();
        world.TryGetSystem<SpawnedSystem<Slot9>>(out _).Should().BeTrue();
    }

    [Fact]
    public void World_AddSystemCore_WithFixedCadence_SetsEntryCadenceToFixed()
    {
        var world = new World();
        var registration = world.AddSystemCore(typeof(RuntimeCadenceProbeSystem), access: null, _ => new RuntimeCadenceProbeSystem(), [], [], cadence: SystemCadence.Fixed);

        registration.Entry.Cadence.Should().Be(SystemCadence.Fixed);
    }
}

file sealed class RuntimeCadenceProbeSystem : EcsSystem { protected override void Execute(World world, Time time) { } }
