using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// The <see cref="Tracked"/> (<see cref="IWorld.TrackChanges{T}"/>) dimension and the
/// one-at-a-time component-add variants — both Wyrd.Ecs-only, with no Friflo or fennecs
/// equivalent, so they don't belong on the shared
/// <see cref="Comparison.EntityLifecycle.EntityLifecycleBenchmarks"/> comparison class. Needs a
/// fresh <see cref="World"/> per invocation, same reasoning as
/// <see cref="Comparison.EntityLifecycle.EntityLifecycleBenchmarks"/>.
/// </summary>
[MemoryDiagnoser]
public class TrackedEntityLifecycleBenchmarks
{
    [Params(false, true)]
    public bool Tracked { get; set; }

    private World _world = null!;
    private Entity _toDispose;

    [IterationSetup]
    public void IterationSetup()
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

        _toDispose = _world.Commands.CreateEntity(new Position());
        _world.ApplyCommands();
    }

    [Benchmark(Baseline = true)]
    public Entity CreateBareEntity()
    {
        var entity = _world.Commands.CreateEntity();
        _world.ApplyCommands();
        return entity;
    }

    [Benchmark]
    public Entity CreateOneComponentEntity()
    {
        var entity = _world.Commands.CreateEntity(new Position());
        _world.ApplyCommands();
        return entity;
    }

    [Benchmark]
    public Entity CreateFourComponentEntity()
    {
        var entity = _world.Commands.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload());
        _world.ApplyCommands();
        return entity;
    }

    [Benchmark]
    public Entity CreateEightComponentEntity()
    {
        var entity = _world.Commands.CreateEntity(
            new Position(), new Velocity(), new Health(), new BulkPayload(),
            new Padding1(), new Padding2(), new Padding3(), new Padding4());
        _world.ApplyCommands();
        return entity;
    }

    [Benchmark]
    public Entity CreateFourComponentEntity_OneAtATime()
    {
        var entity = _world.Commands.CreateEntity();
        _world.Commands.AddComponent(entity, new Position());
        _world.Commands.AddComponent(entity, new Velocity());
        _world.Commands.AddComponent(entity, new Health());
        _world.Commands.AddComponent(entity, new BulkPayload());
        _world.ApplyCommands();
        return entity;
    }

    [Benchmark]
    public Entity CreateEightComponentEntity_OneAtATime()
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
        _world.ApplyCommands();
        return entity;
    }

    [Benchmark]
    public void DisposeEntity()
    {
        _world.Commands.DestroyEntity(_toDispose);
        _world.ApplyCommands();
    }
}
