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
    public Entity CreateOneComponentEntity() => _world.CreateEntity(new Position());

    [Benchmark]
    public Entity CreateFourComponentEntity() =>
        _world.CreateEntity(new Position(), new Velocity(), new Health(), new Payload8());

    [Benchmark]
    public Entity CreateEightComponentEntity() =>
        _world.CreateEntity(
            new Position(), new Velocity(), new Health(), new Payload8(),
            new Filler1(), new Filler2(), new Filler3(), new Filler4());

    /// <summary>
    /// Creates an entity empty, then adds each component one at a time, moving
    /// through an intermediate archetype per add. Kept alongside
    /// <see cref="CreateFourComponentEntity"/> to show the cost of that pattern
    /// against the batched <c>CreateEntity{T...}</c> overloads directly.
    /// </summary>
    [Benchmark]
    public Entity CreateFourComponentEntity_OneAtATime()
    {
        var entity = _world.CreateEntity();
        _world.AddComponent<Position>(entity);
        _world.AddComponent<Velocity>(entity);
        _world.AddComponent<Health>(entity);
        _world.AddComponent<Payload8>(entity);
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
