using BenchmarkDotNet.Attributes;

namespace Comparison.MultiQueryTick;

[MemoryDiagnoser]
public partial class MultiQueryTickBenchmarks
{
    public const int EntityCountPerQuery = 10_000;

    [GlobalSetup]
    public void Setup() => BenchmarkOperations.SetupContexts(this);

    [GlobalCleanup]
    public void Cleanup() => BenchmarkOperations.CleanupContexts(this);
}
