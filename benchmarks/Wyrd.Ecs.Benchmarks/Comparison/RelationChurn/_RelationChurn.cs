using BenchmarkDotNet.Attributes;

namespace Comparison.RelationChurn;

/// <summary>
/// Head-to-head add/remove churn for one relation edge between the same two entities.
/// Friflo and fennecs bake the target into the relation's archetype identity, so every
/// edge add/remove is an archetype move; Wyrd's <c>Wyrd_AddRemoveRelation</c> stores
/// edges in a dictionary/set-backed component instead.
/// </summary>
[MemoryDiagnoser]
public partial class RelationChurnBenchmarks
{
    [GlobalSetup]
    public void Setup() => BenchmarkOperations.SetupContexts(this);

    [GlobalCleanup]
    public void Cleanup() => BenchmarkOperations.CleanupContexts(this);
}
