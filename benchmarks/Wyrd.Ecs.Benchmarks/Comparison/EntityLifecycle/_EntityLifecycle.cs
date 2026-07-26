using BenchmarkDotNet.Attributes;

namespace Comparison.EntityLifecycle;

[MemoryDiagnoser]
public partial class EntityLifecycleBenchmarks
{
    [IterationSetup]
    public void Setup() => BenchmarkOperations.SetupContexts(this);

    [IterationCleanup]
    public void Cleanup() => BenchmarkOperations.CleanupContexts(this);
}
