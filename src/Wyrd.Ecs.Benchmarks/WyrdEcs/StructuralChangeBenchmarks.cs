using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

[MemoryDiagnoser]
public class StructuralChangeBenchmarks
{
    [Params(false, true)]
    public bool Tracked { get; set; }

    private World _world = null!;
    private Entity _entity;
    private ChangeConsumer<Position>? _positionConsumer;
    private ChangeConsumer<BulkPayload>? _bulkPayloadConsumer;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _world = new World();
        _entity = _world.CreateEntity();
        _world.AddComponent<Position>(_entity);
        _world.AddComponent<Velocity>(_entity);

        if (Tracked)
        {
            _positionConsumer = _world.RegisterChangeConsumer<Position>();
            _bulkPayloadConsumer = _world.RegisterChangeConsumer<BulkPayload>();
        }
    }

    /// <summary>
    /// Every benchmark here advances its registered consumer(s) every call, the same
    /// way a correctly-written consumer always does after handling a batch — a no-op
    /// when <see cref="Tracked"/> is false, since nothing is registered. Without this,
    /// the change log grows unboundedly for the whole benchmark job and
    /// <c>TrimBefore</c>'s binary search cost grows with it, measuring a benchmark
    /// artifact rather than a representative tracked cost.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void MutateExistingComponent()
    {
        _world.AdvanceTick();
        ref var position = ref _world.GetComponent<Position>(_entity);
        position.X += 0f;
        _positionConsumer?.Advance(_world.CurrentTick);
    }

    [Benchmark]
    public void AddRemoveComponent_ArchetypeMove()
    {
        _world.AdvanceTick();
        _world.AddComponent<BulkPayload>(_entity);
        _world.RemoveComponent<BulkPayload>(_entity);
        _bulkPayloadConsumer?.Advance(_world.CurrentTick);
    }

    /// <summary>
    /// Tags carry no data and have no <see cref="ChangeConsumer{T}"/> equivalent
    /// (<see cref="World.RegisterChangeConsumer{T}"/> requires <c>IComponent</c>, not
    /// <c>ITag</c>), so tag churn itself has no tracked-vs-untracked cost. It still runs
    /// under both <see cref="Tracked"/> values because <see cref="Tracked"/> is a
    /// class-level dimension, not a per-benchmark one — and the two rows are *not* the
    /// same: every benchmark here calls <see cref="World.AdvanceTick"/> first, which pays
    /// real per-tick retention-scan cost whenever other types in the same world
    /// (<see cref="Position"/>, <see cref="BulkPayload"/> here) have live consumers,
    /// regardless of what this specific operation touches. That is a genuine, honest cost
    /// of ticking a world that tracks anything at all, not an artifact.
    /// </summary>
    [Benchmark]
    public void AddRemoveTag_ArchetypeMove()
    {
        _world.AdvanceTick();
        _world.AddTag<Marker>(_entity);
        _world.RemoveTag<Marker>(_entity);
    }
}
