using BenchmarkDotNet.Attributes;
using fennecs;
using Comparison.Fennecs;
// fennecs ships its own generic Link<T> (entity-object relations), colliding with our
// vocabulary's non-generic Link. Disambiguate in favor of ours.
using Link = Comparison.Fennecs.Link;

namespace Comparison.RelationChurn;

public partial class RelationChurnBenchmarks
{
    private sealed class FennecsContext
    {
        public readonly World World = new();
        public readonly Entity A;
        public readonly Entity B;

        public FennecsContext()
        {
            A = World.Spawn();
            B = World.Spawn();
        }
    }

    [Context] private FennecsContext _fennecs = null!;

    [Benchmark]
    public void Fennecs_AddRemoveRelation()
    {
        _fennecs.A.Add(new Link { Weight = 1f }, _fennecs.B);
        _fennecs.A.Remove<Link>(_fennecs.B);
    }
}
