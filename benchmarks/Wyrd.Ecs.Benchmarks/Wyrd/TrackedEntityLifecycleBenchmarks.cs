using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// The <see cref="Tracked"/> dimension and one-at-a-time component-add variants, both
/// Wyrd.Ecs-only with no Friflo/fennecs equivalent, so they don't belong on the shared
/// <see cref="Comparison.EntityLifecycle.EntityLifecycleBenchmarks"/> comparison class. Same
/// <c>[GlobalSetup]</c>/batching/<c>[SimpleJob(invocationCount: 1)]</c> reasoning as that
/// class. The <c>Create*</c> methods additionally reset via <see cref="ResetWorld"/> every
/// iteration so later iterations don't measure into an already-grown world;
/// <see cref="DisposeEntity"/> is excluded since it destroys everything it creates.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(invocationCount: 1)]
public class TrackedEntityLifecycleBenchmarks
{
    private const int EntityCount = Comparison.EntityLifecycle.EntityLifecycleBenchmarks.EntityCount;

    [Params(false, true)]
    public bool Tracked { get; set; }

    private World _world = null!;

    /// <summary>Reused scratch space for <see cref="DisposeEntity"/>: sized once, never reallocated, so it doesn't contaminate that method's own allocation measurement.</summary>
    private Entity[] _disposeScratch = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        ResetWorld();
        _disposeScratch = new Entity[EntityCount];
    }

    [IterationSetup(Targets = [
        nameof(CreateBareEntity), nameof(CreateOneComponentEntity), nameof(CreateFourComponentEntity),
        nameof(CreateEightComponentEntity), nameof(CreateFourComponentEntity_OneAtATime), nameof(CreateEightComponentEntity_OneAtATime)])]
    public void ResetWorld()
    {
        _world = new World();

        if (Tracked)
        {
            _world.TrackChanges<Position>();
            _world.TrackChanges<Velocity>();
            _world.TrackChanges<Health>();
            _world.TrackChanges<BulkPayload>();
            _world.TrackChanges<Padding1>();
            _world.TrackChanges<Padding2>();
            _world.TrackChanges<Padding3>();
            _world.TrackChanges<Padding4>();
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = EntityCount)]
    public void CreateBareEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _world.Commands.CreateEntity();
        _world.ApplyCommands();
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void CreateOneComponentEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _world.Commands.CreateEntity(new Position());
        _world.ApplyCommands();
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void CreateFourComponentEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _world.Commands.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload());
        _world.ApplyCommands();
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void CreateEightComponentEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _world.Commands.CreateEntity(
                new Position(), new Velocity(), new Health(), new BulkPayload(),
                new Padding1(), new Padding2(), new Padding3(), new Padding4());
        _world.ApplyCommands();
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void CreateFourComponentEntity_OneAtATime()
    {
        for (var i = 0; i < EntityCount; i++)
        {
            Entity entity = _world.Commands.CreateEntity();
            _world.Commands.AddComponent(entity, new Position());
            _world.Commands.AddComponent(entity, new Velocity());
            _world.Commands.AddComponent(entity, new Health());
            _world.Commands.AddComponent(entity, new BulkPayload());
        }
        _world.ApplyCommands();
    }

    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void CreateEightComponentEntity_OneAtATime()
    {
        for (var i = 0; i < EntityCount; i++)
        {
            Entity entity = _world.Commands.CreateEntity();
            _world.Commands.AddComponent(entity, new Position());
            _world.Commands.AddComponent(entity, new Velocity());
            _world.Commands.AddComponent(entity, new Health());
            _world.Commands.AddComponent(entity, new BulkPayload());
            _world.Commands.AddComponent(entity, new Padding1());
            _world.Commands.AddComponent(entity, new Padding2());
            _world.Commands.AddComponent(entity, new Padding3());
            _world.Commands.AddComponent(entity, new Padding4());
        }
        _world.ApplyCommands();
    }

    /// <summary>Create-then-destroy pairs, self-resetting. See <see cref="Comparison.EntityLifecycle.EntityLifecycleBenchmarks.Wyrd_DisposeEntity"/>'s docs for why.</summary>
    [Benchmark(OperationsPerInvoke = EntityCount)]
    public void DisposeEntity()
    {
        for (var i = 0; i < EntityCount; i++)
            _disposeScratch[i] = _world.Commands.CreateEntity();
        _world.ApplyCommands();

        for (var i = 0; i < EntityCount; i++)
            _world.Commands.DestroyEntity(_disposeScratch[i]);
        _world.ApplyCommands();
    }
}
