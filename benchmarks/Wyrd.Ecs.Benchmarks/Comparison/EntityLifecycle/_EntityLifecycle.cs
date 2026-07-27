using BenchmarkDotNet.Attributes;

namespace Comparison.EntityLifecycle;

/// <summary>
/// <c>[GlobalSetup]</c>/<c>[GlobalCleanup]</c>, not <c>[IterationSetup]</c>/<c>[IterationCleanup]</c> —
/// BenchmarkDotNet's own docs warn against IterationSetup for microbenchmarks ("it can spoil the
/// results") because it forces <c>InvocationCount=1</c>, and every method here runs in
/// nanoseconds, nowhere near the ~100ms-per-invocation floor that warning assumes. Each
/// <c>[Context]</c> is built once and then genuinely reused across every invocation of its
/// backend's benchmark methods — a steadily growing <c>World</c>/<c>Store</c> is what a real
/// entity's lifetime actually looks like, not a freshly-rebuilt one before every single
/// creation. The one method this shape doesn't fit for free is <c>*_DisposeEntity</c>, which
/// needs a live target every call — see each backend file's own comment on how it stays
/// self-resetting without a per-iteration reset.
///
/// <para>
/// Every <c>[Benchmark]</c> method also creates/destroys <see cref="EntityCount"/> entities
/// per invocation (via <c>OperationsPerInvoke</c>), not one — the same batching
/// <a href="https://github.com/Doraku/Ecs.CSharp.Benchmark">Doraku/Ecs.CSharp.Benchmark</a>'s
/// suite uses. This isn't just about wall-clock speed: a single bare-entity create is a few
/// tens of nanoseconds, comparable to BenchmarkDotNet's own per-invocation dispatch overhead,
/// so an unbatched measurement has poor signal-to-noise and needs many iterations to average
/// that noise away (or, under a forced fixed iteration count, may not converge cleanly at
/// all). Batching <see cref="EntityCount"/> operations into each measured invocation makes
/// each sample chunkier relative to that fixed overhead, so the adaptive pilot converges in
/// fewer iterations for the same statistical confidence.
/// </para>
///
/// <para>
/// <c>[SimpleJob(invocationCount: 1)]</c> pins <c>UnrollFactor</c> to 1 alongside it — without
/// this, BenchmarkDotNet's own adaptive engine still tries to multiply each already-
/// <see cref="EntityCount"/>-sized invocation further to reach its target iteration duration
/// (observed: 2048x per iteration, ~20 million entities per single measured iteration), and
/// since every <c>[Context]</c> here is never reset (see above), that compounds across the
/// whole run into billions of entities — confirmed by running this without the pin: Friflo's
/// and fennecs' internal array-growth math overflowed (<c>OverflowException</c>,
/// <c>ArgumentOutOfRangeException</c>) or ran the process out of memory outright. Pinning
/// <c>InvocationCount</c> to exactly 1 means one iteration = one <see cref="EntityCount"/>-sized
/// batch, no further multiplication — bounding total growth to (iteration count) ×
/// <see cref="EntityCount"/> instead of that times an unbounded, adaptively-chosen unroll
/// factor too.
/// </para>
/// </summary>
[MemoryDiagnoser]
[SimpleJob(invocationCount: 1)]
public partial class EntityLifecycleBenchmarks
{
    /// <summary>Entities created/destroyed per invocation — see this class's own docs for why batching matters here.</summary>
    public const int EntityCount = 10_000;

    [GlobalSetup]
    public void Setup() => BenchmarkOperations.SetupContexts(this);

    [GlobalCleanup]
    public void Cleanup() => BenchmarkOperations.CleanupContexts(this);
}
