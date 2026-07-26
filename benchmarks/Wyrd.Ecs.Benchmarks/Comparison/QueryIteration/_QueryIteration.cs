using BenchmarkDotNet.Attributes;

namespace Comparison.QueryIteration;

[MemoryDiagnoser]
public partial class QueryIterationBenchmarks
{
    public const int EntityCount = 10_000;

    [Params(false, true)]
    public bool Fragmented { get; set; }

    [GlobalSetup]
    public void Setup() => BenchmarkOperations.SetupContexts(this, EntityCount, Fragmented);

    [GlobalCleanup]
    public void Cleanup() => BenchmarkOperations.CleanupContexts(this);
}
