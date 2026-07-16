using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

[MemoryDiagnoser]
public class StructuralChangeBenchmarks
{
    private World _world = null!;
    private Entity _entity;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _world = new World();
        _entity = _world.CreateEntity();
        _world.AddComponent<Position>(_entity);
        _world.AddComponent<Velocity>(_entity);
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
