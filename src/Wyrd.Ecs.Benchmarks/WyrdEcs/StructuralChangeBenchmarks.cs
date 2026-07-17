using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

[MemoryDiagnoser]
public class StructuralChangeBenchmarks
{
    private World _world = null!;
    private Entity _entity;

    private World _worldWithConsumer = null!;
    private Entity _entityWithConsumer;
    private ChangeConsumer<Position> _consumer = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _world = new World();
        _entity = _world.CreateEntity();
        _world.AddComponent<Position>(_entity);
        _world.AddComponent<Velocity>(_entity);

        _worldWithConsumer = new World();
        _entityWithConsumer = _worldWithConsumer.CreateEntity();
        _worldWithConsumer.AddComponent<Position>(_entityWithConsumer);
        _worldWithConsumer.AddComponent<Velocity>(_entityWithConsumer);
        _consumer = _worldWithConsumer.RegisterChangeConsumer<Position>();
    }

    [Benchmark(Baseline = true)]
    public void MutateExistingComponent_NoRegisteredConsumer()
    {
        _world.AdvanceTick();
        ref var position = ref _world.GetComponent<Position>(_entity);
        position.X += 0f;
    }

    /// <summary>
    /// The consumer advances every call, the same way a correctly-written consumer
    /// always does after handling a batch. Without this, the log grows unboundedly for
    /// the whole benchmark job and <c>TrimBefore</c>'s binary search cost grows with
    /// it, measuring a benchmark artifact rather than a representative tracked cost.
    /// </summary>
    [Benchmark]
    public void MutateExistingComponent_WithRegisteredConsumer()
    {
        _worldWithConsumer.AdvanceTick();
        ref var position = ref _worldWithConsumer.GetComponent<Position>(_entityWithConsumer);
        position.X += 0f;
        _consumer.Advance(_worldWithConsumer.CurrentTick);
    }

    [Benchmark]
    public void AddRemoveComponent_ArchetypeMove()
    {
        _world.AddComponent<Payload8>(_entity);
        _world.RemoveComponent<Payload8>(_entity);
    }

    [Benchmark]
    public void AddRemoveTag_ArchetypeMove()
    {
        _world.AddTag<Marker>(_entity);
        _world.RemoveTag<Marker>(_entity);
    }
}
