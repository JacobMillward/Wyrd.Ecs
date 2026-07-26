using BenchmarkDotNet.Attributes;

namespace Comparison.RelationChurn;

/// <summary>
/// No <c>Wyrd.cs</c> in this folder — <see cref="Wyrd.Ecs.IWorld"/> has no relation API, so
/// there's nothing on the Wyrd.Ecs side to benchmark yet. This stays a Friflo/fennecs-only
/// informational baseline, same as it was before the Comparison/Wyrd restructure — see
/// docs/superpowers/specs/2026-07-16-wyrd-ecs-synthetic-benchmark-suite-design.md's category 6.
/// </summary>
[MemoryDiagnoser]
public partial class RelationChurnBenchmarks
{
    [GlobalSetup]
    public void Setup() => BenchmarkOperations.SetupContexts(this);

    [GlobalCleanup]
    public void Cleanup() => BenchmarkOperations.CleanupContexts(this);
}
