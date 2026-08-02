using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Comparison.RelationChurn;

public partial class RelationChurnBenchmarks
{
    private sealed class WyrdContext
    {
        public readonly World World = new();
        public readonly Entity A;
        public readonly Entity B;

        public WyrdContext()
        {
            A = World.Commands.CreateEntity();
            B = World.Commands.CreateEntity();
            World.ApplyCommands();
        }
    }

    [Context] private WyrdContext _wyrd = null!;

    /// <summary>
    /// Add then remove one edge between the same two entities, deferred through
    /// <see cref="CommandBuffer"/> and applied separately, matching the shape of the
    /// sibling Friflo/fennecs benchmarks in this file. Exercises Wyrd's O(1) dictionary/set
    /// edge storage rather than an archetype-fragmenting move.
    /// </summary>
    [Benchmark]
    public void Wyrd_AddRemoveRelation()
    {
        _wyrd.World.Commands.AddRelation(_wyrd.A, _wyrd.B, new Link { Weight = 1f });
        _wyrd.World.ApplyCommands();
        _wyrd.World.Commands.RemoveRelation<Link>(_wyrd.A, _wyrd.B);
        _wyrd.World.ApplyCommands();
    }
}
