using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

[MemoryDiagnoser]
public class StructuralChangeBenchmarks
{
    private World _noConsumer = null!;
    private World _withConsumer = null!;
    private Entity _entity;
    private ChangeConsumer<Position> _positionConsumer = null!;
    private ChangeConsumer<BulkPayload> _bulkPayloadConsumer = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        (_noConsumer, _withConsumer, _entity) = BenchmarkWorlds.CreatePaired(world =>
        {
            var entity = world.CreateEntity();
            world.AddComponent<Position>(entity);
            world.AddComponent<Velocity>(entity);
            return entity;
        });

        _positionConsumer = _withConsumer.RegisterChangeConsumer<Position>();
        _bulkPayloadConsumer = _withConsumer.RegisterChangeConsumer<BulkPayload>();
    }

    [Benchmark(Baseline = true)]
    public void MutateExistingComponent_NoRegisteredConsumer() => MutateExistingComponent(_noConsumer);

    /// <summary>
    /// Every `_WithRegisteredConsumer` benchmark in this suite advances its consumer(s)
    /// every call, the same way a correctly-written consumer always does after handling a
    /// batch. Without this, the change log grows unboundedly for the whole benchmark job
    /// and <c>TrimBefore</c>'s binary search cost grows with it, measuring a benchmark
    /// artifact rather than a representative tracked cost.
    /// </summary>
    [Benchmark]
    public void MutateExistingComponent_WithRegisteredConsumer()
    {
        MutateExistingComponent(_withConsumer);
        _positionConsumer.Advance(_withConsumer.CurrentTick);
    }

    private void MutateExistingComponent(World world)
    {
        world.AdvanceTick();
        ref var position = ref world.GetComponent<Position>(_entity);
        position.X += 0f;
    }

    [Benchmark]
    public void AddRemoveComponent_ArchetypeMove_NoRegisteredConsumer() => AddRemoveComponent(_noConsumer);

    [Benchmark]
    public void AddRemoveComponent_ArchetypeMove_WithRegisteredConsumer()
    {
        AddRemoveComponent(_withConsumer);
        _bulkPayloadConsumer.Advance(_withConsumer.CurrentTick);
    }

    private void AddRemoveComponent(World world)
    {
        world.AdvanceTick();
        world.AddComponent<BulkPayload>(_entity);
        world.RemoveComponent<BulkPayload>(_entity);
    }

    /// <summary>
    /// Tags carry no data and have no <see cref="ChangeConsumer{T}"/> equivalent
    /// (<see cref="World.RegisterChangeConsumer{T}"/> requires <c>IComponent</c>, not
    /// <c>ITag</c>) — there is no tracked state for a tag add/remove to cost more, so this
    /// benchmark has no `_WithRegisteredConsumer` counterpart.
    /// </summary>
    [Benchmark]
    public void AddRemoveTag_ArchetypeMove()
    {
        _noConsumer.AdvanceTick();
        _noConsumer.AddTag<Marker>(_entity);
        _noConsumer.RemoveTag<Marker>(_entity);
    }
}
