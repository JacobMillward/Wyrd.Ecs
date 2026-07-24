using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

/// <summary>
/// Unlike <see cref="StructuralChangeBenchmarks"/> and <see cref="QueryIterationBenchmarks"/>,
/// this class can't build <see cref="Tracked"/>'s world once in <c>[GlobalSetup]</c>:
/// every benchmark here mutates the world's entity/archetype table itself (that's the
/// thing being measured), so it needs a fresh <see cref="World"/> per invocation,
/// rebuilt in <c>[IterationSetup]</c>. Every benchmark here queues through
/// <see cref="Commands"/> and calls <see cref="World.ApplyCommands"/> in the same
/// method — that round trip is now the real, only cost of creating or destroying an
/// entity, not something to hide from the measurement.
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

    /// <summary>
    /// Creates an entity empty, then queues each component add one at a time, moving
    /// through an intermediate archetype per add once applied. Kept alongside
    /// <see cref="CreateFourComponentEntity"/> to show the cost of that pattern against
    /// the batched <c>Commands.CreateEntity{T...}</c> overload directly, in both tracked
    /// and untracked form.
    /// </summary>
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

    /// <inheritdoc cref="CreateFourComponentEntity_OneAtATime"/>
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
