using BenchmarkDotNet.Attributes;
using Friflo.Engine.ECS;

namespace Wyrd.Ecs.Benchmarks.Friflo;

[MemoryDiagnoser]
public class RelationBenchmarks
{
    private EntityStore _store = null!;
    private Entity _a;
    private Entity _b;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _store = new EntityStore();
        _a = _store.CreateEntity();
        _b = _store.CreateEntity();
    }

    [Benchmark]
    public void AddRemoveRelation()
    {
        _a.AddRelation(new Link { Target = _b, Weight = 1f });
        _a.RemoveRelation<Link, Entity>(_b);
    }
}
