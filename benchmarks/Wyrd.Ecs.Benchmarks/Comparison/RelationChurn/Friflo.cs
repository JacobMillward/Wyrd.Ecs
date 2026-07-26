using BenchmarkDotNet.Attributes;
using Friflo.Engine.ECS;
using Comparison.Friflo;

namespace Comparison.RelationChurn;

public partial class RelationChurnBenchmarks
{
    private sealed class FrifloContext
    {
        public readonly EntityStore Store = new();
        public readonly Entity A;
        public readonly Entity B;

        public FrifloContext()
        {
            A = Store.CreateEntity();
            B = Store.CreateEntity();
        }
    }

    [Context] private FrifloContext _friflo = null!;

    [Benchmark]
    public void Friflo_AddRemoveRelation()
    {
        _friflo.A.AddRelation(new Link { Target = _friflo.B, Weight = 1f });
        _friflo.A.RemoveRelation<Link, Entity>(_friflo.B);
    }
}
