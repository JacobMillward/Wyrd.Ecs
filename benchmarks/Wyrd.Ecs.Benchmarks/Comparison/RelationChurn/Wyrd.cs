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
    /// Add then remove one edge between the same two entities, each deferred through
    /// <see cref="CommandBuffer"/> and applied separately — the closest same-shape match
    /// to <c>Fennecs_AddRemoveRelation</c>/<c>Friflo_AddRemoveRelation</c>'s two immediate
    /// calls in this same file's sibling benchmarks. Unlike those two engines, an edge
    /// here is never baked into the archetype signature by target, so this exercises the
    /// O(1) dictionary/set path described in
    /// docs/superpowers/specs/2026-07-30-entity-relationships-design.md, not an
    /// archetype-fragmenting pairs move.
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
