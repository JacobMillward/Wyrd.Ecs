using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

/// <summary>
/// Unlike <see cref="StructuralChangeBenchmarks"/> and <see cref="QueryIterationBenchmarks"/>,
/// this class can't build <see cref="Tracked"/>'s world once in <c>[GlobalSetup]</c>:
/// every benchmark here mutates the world's entity/archetype table itself (that's the
/// thing being measured), so it needs a fresh <see cref="World"/> per invocation,
/// rebuilt in <c>[IterationSetup]</c>.
/// </summary>
[MemoryDiagnoser]
public class EntityLifecycleBenchmarks
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

        _toDispose = _world.CreateEntity();
        _world.AddComponent<Position>(_toDispose);
    }

    [Benchmark(Baseline = true)]
    public Entity CreateBareEntity() => _world.CreateEntity();

    [Benchmark]
    public Entity CreateOneComponentEntity() => _world.CreateEntity(new Position());

    [Benchmark]
    public Entity CreateFourComponentEntity() =>
        _world.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload());

    [Benchmark]
    public Entity CreateEightComponentEntity() =>
        _world.CreateEntity(
            new Position(), new Velocity(), new Health(), new BulkPayload(),
            new Padding1(), new Padding2(), new Padding3(), new Padding4());

    /// <summary>
    /// Creates an entity empty, then adds each component one at a time, moving through an
    /// intermediate archetype per add. Kept alongside <see cref="CreateFourComponentEntity"/>
    /// to show the cost of that pattern against the batched <c>CreateEntity{T...}</c>
    /// overload directly, in both tracked and untracked form.
    /// </summary>
    [Benchmark]
    public Entity CreateFourComponentEntity_OneAtATime()
    {
        var entity = _world.CreateEntity();
        _world.AddComponent<Position>(entity);
        _world.AddComponent<Velocity>(entity);
        _world.AddComponent<Health>(entity);
        _world.AddComponent<BulkPayload>(entity);
        return entity;
    }

    /// <inheritdoc cref="CreateFourComponentEntity_OneAtATime"/>
    [Benchmark]
    public Entity CreateEightComponentEntity_OneAtATime()
    {
        var entity = _world.CreateEntity();
        _world.AddComponent<Position>(entity);
        _world.AddComponent<Velocity>(entity);
        _world.AddComponent<Health>(entity);
        _world.AddComponent<BulkPayload>(entity);
        _world.AddComponent<Padding1>(entity);
        _world.AddComponent<Padding2>(entity);
        _world.AddComponent<Padding3>(entity);
        _world.AddComponent<Padding4>(entity);
        return entity;
    }

    [Benchmark]
    public void DisposeEntity() => _world.DestroyEntity(_toDispose);
}
