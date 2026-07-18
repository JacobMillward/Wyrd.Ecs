using BenchmarkDotNet.Attributes;
using Friflo.Engine.ECS;

namespace FrifloBenchmarks;

[MemoryDiagnoser]
public class StructuralChangeBenchmarks
{
    private EntityStore _store = null!;
    private Entity _entity;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _store = new EntityStore();
        _entity = _store.CreateEntity(new Position(), new Velocity());
    }

    [Benchmark(Baseline = true)]
    public void MutateExistingComponent()
    {
        ref var position = ref _entity.GetComponent<Position>();
        position.X += 0f;
    }

    [Benchmark]
    public void AddRemoveComponent_ArchetypeMove()
    {
        _entity.AddComponent(new BulkPayload());
        _entity.RemoveComponent<BulkPayload>();
    }

    [Benchmark]
    public void AddRemoveTag_ArchetypeMove()
    {
        _entity.AddTag<Marker>();
        _entity.RemoveTag<Marker>();
    }
}
