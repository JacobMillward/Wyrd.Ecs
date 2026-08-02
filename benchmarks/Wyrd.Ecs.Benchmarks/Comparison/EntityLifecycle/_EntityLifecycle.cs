using BenchmarkDotNet.Attributes;

namespace Comparison.EntityLifecycle;

/// <summary>
/// Uses <c>[GlobalSetup]</c>/<c>[GlobalCleanup]</c>, not <c>[IterationSetup]</c>: each
/// <c>[Context]</c> is built once and reused across every invocation, matching how a real
/// entity's lifetime looks (a steadily growing World/Store, not a freshly-rebuilt one per
/// creation). <c>*_DisposeEntity</c> is the exception, needing a live target every call;
/// see each backend file's own comment for how it stays self-resetting.
///
/// <para>
/// Each <c>[Benchmark]</c> method creates/destroys <see cref="EntityCount"/> entities per
/// invocation (via <c>OperationsPerInvoke</c>), not one: a single bare-entity create is a
/// few tens of nanoseconds, comparable to BenchmarkDotNet's own dispatch overhead, so an
/// unbatched measurement has poor signal-to-noise.
/// </para>
///
/// <para>
/// <c>[SimpleJob(invocationCount: 1)]</c> pins <c>UnrollFactor</c> to 1. Without it,
/// BenchmarkDotNet's adaptive engine multiplies each already-<see cref="EntityCount"/>-sized
/// invocation further, and since every <c>[Context]</c> here is never reset, that compounds
/// into unbounded growth. Confirmed by running without the pin: Friflo's and fennecs'
/// array-growth math overflowed or ran the process out of memory.
/// </para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(invocationCount: 1)]
public partial class EntityLifecycleBenchmarks
{
    /// <summary>Entities created/destroyed per invocation. See this class's own docs for why batching matters here.</summary>
    public const int EntityCount = 10_000;

    [GlobalSetup]
    public void Setup() => BenchmarkOperations.SetupContexts(this);

    [GlobalCleanup]
    public void Cleanup() => BenchmarkOperations.CleanupContexts(this);
}
