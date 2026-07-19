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

    [GlobalSetup]
    public void GlobalSetup()
    {
        _world = new World();
        _entity = _world.CreateEntity();
        _world.AddComponent<Position>(_entity);
        _world.AddComponent<Velocity>(_entity);

        if (Tracked)
        {
            _world.TrackChanges<Position>();
            _world.TrackChanges<BulkPayload>();
        }
    }

    [Benchmark(Baseline = true)]
    public void MutateExistingComponent()
    {
        _world.AdvanceTick();
        ref var position = ref _world.GetComponent<Position>(_entity);
        position.X += 0f;
    }

    [Benchmark]
    public void AddRemoveComponent_ArchetypeMove()
    {
        _world.AdvanceTick();
        _world.AddComponent<BulkPayload>(_entity);
        _world.RemoveComponent<BulkPayload>(_entity);
    }

    /// <summary>
    /// Tags carry no data and have no <see cref="IWorld.TrackChanges{T}"/> equivalent
    /// (it requires <c>IComponent</c>, not <c>ITag</c>), so tag churn itself has no
    /// tracked-vs-untracked cost. It still runs under both <see cref="Tracked"/> values
    /// because <see cref="Tracked"/> is a class-level dimension, not a per-benchmark one.
    /// </summary>
    [Benchmark]
    public void AddRemoveTag_ArchetypeMove()
    {
        _world.AdvanceTick();
        _world.AddTag<Marker>(_entity);
        _world.RemoveTag<Marker>(_entity);
    }
}
