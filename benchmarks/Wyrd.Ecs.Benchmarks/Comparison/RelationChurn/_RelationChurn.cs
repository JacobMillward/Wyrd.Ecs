using BenchmarkDotNet.Attributes;

namespace Comparison.RelationChurn;

/// <summary>
/// Head-to-head add/remove churn for one relation edge between the same two entities.
/// Friflo/fennecs bake the target into the relation's archetype identity (see
/// <c>Fennecs.cs</c>/<c>Friflo.cs</c> in this folder), so every edge add/remove there is an
/// archetype move; Wyrd's <c>Wyrd_AddRemoveRelation</c> (see <c>Wyrd.cs</c>) stores edges in
/// a dictionary/set-backed component instead — see
/// docs/superpowers/specs/2026-07-30-entity-relationships-design.md for the full rationale.
/// </summary>
[MemoryDiagnoser]
public partial class RelationChurnBenchmarks
{
    [GlobalSetup]
    public void Setup() => BenchmarkOperations.SetupContexts(this);

    [GlobalCleanup]
    public void Cleanup() => BenchmarkOperations.CleanupContexts(this);
}
