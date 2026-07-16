using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

[MemoryDiagnoser]
public class EntityLifecycleBenchmarks
{
    private World _world = null!;
    private Entity _toDispose;

    [IterationSetup]
    public void IterationSetup()
    {
        _world = new World();
        _toDispose = _world.CreateEntity();
        _world.AddComponent<Position>(_toDispose);
    }

    [Benchmark(Baseline = true)]
    public Entity CreateBareEntity() => _world.CreateEntity();

    [Benchmark]
    public Entity CreateOneComponentEntity()
    {
        var entity = _world.CreateEntity();
        _world.AddComponent<Position>(entity);
        return entity;
    }

    [Benchmark]
    public Entity CreateFourComponentEntity()
    {
        var entity = _world.CreateEntity();
        _world.AddComponent<Position>(entity);
        _world.AddComponent<Velocity>(entity);
        _world.AddComponent<Health>(entity);
        _world.AddComponent<Payload8>(entity);
        return entity;
    }

    [Benchmark]
    public Entity CreateEightComponentEntity()
    {
        var entity = _world.CreateEntity();
        _world.AddComponent<Position>(entity);
        _world.AddComponent<Velocity>(entity);
        _world.AddComponent<Health>(entity);
        _world.AddComponent<Payload8>(entity);
        _world.AddComponent<Filler1>(entity);
        _world.AddComponent<Filler2>(entity);
        _world.AddComponent<Filler3>(entity);
        _world.AddComponent<Filler4>(entity);
        return entity;
    }

    [Benchmark]
    public void DisposeEntity() => _world.DestroyEntity(_toDispose);
}
