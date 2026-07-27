using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// The <see cref="Tracked"/> (<see cref="IWorld.TrackChanges{T}"/>) dimension and the
/// one-at-a-time component-add variants — both Wyrd.Ecs-only, with no Friflo or fennecs
/// equivalent, so they don't belong on the shared
/// <see cref="Comparison.EntityLifecycle.EntityLifecycleBenchmarks"/> comparison class.
/// <c>[GlobalSetup]</c>, not <c>[IterationSetup]</c>, and <see cref="EntityCount"/> entities
/// per invocation, not one — see that class's docs for both: BenchmarkDotNet's own guidance
/// warns IterationSetup "can spoil the results" for anything faster than ~100ms/invocation,
/// which every method here is by several orders of magnitude, and batching gives the
/// adaptive pilot a better signal-to-noise ratio per sample so it converges faster. One
/// <see cref="World"/> is built once and grows across every invocation — which is exactly why
/// <c>[SimpleJob(invocationCount: 1)]</c> below is required, not optional: without it,
/// BenchmarkDotNet's adaptive engine multiplies each already-<see cref="EntityCount"/>-sized
/// invocation further on top, and since this <see cref="World"/> never resets, that compounds
/// into unbounded growth across the run — see
/// <see cref="Comparison.EntityLifecycle.EntityLifecycleBenchmarks"/>'s docs for the
/// (confirmed by an actual crash: <c>OverflowException</c>/<c>OutOfMemoryException</c>) reasoning.
/// Even with growth bounded per-iteration by that pin, the <c>Create*</c> methods still need
/// <see cref="ResetWorld"/> to run every iteration (not just once via <c>[GlobalSetup]</c>) —
/// otherwise later iterations measure creation into a much bigger, already-grown world than
/// earlier ones, a non-stationary measurement (confirmed: without this, results were
/// internally inconsistent, e.g. the four-component variant reporting cheaper than the
/// one-component one). <see cref="DisposeEntity"/> is excluded from that reset (see its own
/// docs) since it already destroys everything it creates each invocation.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(invocationCount: 1)]
public class TrackedEntityLifecycleBenchmarks
{
    private const int EntityCount = Comparison.EntityLifecycle.EntityLifecycleBenchmarks.EntityCount;

    [Params(false, true)]
    public bool Tracked { get; set; }

    private World _world = null!;

    /// <summary>Reused scratch space for <see cref="DisposeEntity"/> — sized once, never reallocated, so it doesn't contaminate that method's own allocation measurement.</summary>
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
            var entity = _world.Commands.CreateEntity();
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
            var entity = _world.Commands.CreateEntity();
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

    /// <summary>Create-then-destroy pairs, self-resetting — see <see cref="Comparison.EntityLifecycle.EntityLifecycleBenchmarks.Wyrd_DisposeEntity"/>'s docs for why.</summary>
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
