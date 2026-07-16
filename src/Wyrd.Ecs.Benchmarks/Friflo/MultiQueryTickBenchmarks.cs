using BenchmarkDotNet.Attributes;
using Friflo.Engine.ECS;

namespace Wyrd.Ecs.Benchmarks.Friflo;

[MemoryDiagnoser]
public class MultiQueryTickBenchmarks
{
    private const int EntityCountPerQuery = 10_000;

    // One shared store — a real tick runs every System against the same World — but
    // each population carries exactly one, non-overlapping component so the three
    // queries below stay disjoint (see the class-level note in the plan).
    private EntityStore _store = null!;
    private ArchetypeQuery<Position> _positionOnlyQuery = null!;
    private ArchetypeQuery<Velocity> _velocityOnlyQuery = null!;
    private ArchetypeQuery<Health> _healthOnlyQuery = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _store = new EntityStore();

        for (var i = 0; i < EntityCountPerQuery; i++)
            _store.CreateEntity(new Position());

        for (var i = 0; i < EntityCountPerQuery; i++)
            _store.CreateEntity(new Velocity());

        for (var i = 0; i < EntityCountPerQuery; i++)
            _store.CreateEntity(new Health());

        _positionOnlyQuery = _store.Query<Position>();
        _velocityOnlyQuery = _store.Query<Velocity>();
        _healthOnlyQuery = _store.Query<Health>();
    }

    [Benchmark]
    public void RunThreeSystemsOneTick()
    {
        foreach (var (position, entities) in _positionOnlyQuery.Chunks)
            for (var n = 0; n < entities.Length; n++)
                position[n].X += position[n].Y * 0f;

        foreach (var (velocity, entities) in _velocityOnlyQuery.Chunks)
            for (var n = 0; n < entities.Length; n++)
                velocity[n].X += velocity[n].Y * 0f;

        foreach (var (health, entities) in _healthOnlyQuery.Chunks)
            for (var n = 0; n < entities.Length; n++)
                health[n].Current += health[n].Max * 0f;
    }
}
