using BenchmarkDotNet.Attributes;

namespace Comparison.StructuralChange;

[MemoryDiagnoser]
public partial class StructuralChangeBenchmarks
{
    [GlobalSetup]
    public void Setup() => BenchmarkOperations.SetupContexts(this);

    [GlobalCleanup]
    public void Cleanup() => BenchmarkOperations.CleanupContexts(this);
}
