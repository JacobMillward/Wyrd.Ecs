using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Benchmarks <see cref="StagePlanner.BuildStages"/> at increasing system counts. This is
/// the recompute now triggered on every runtime <c>AddSystem</c>/<c>RemoveSystem</c>
/// (deferred, coalesced to at most once per <see cref="World.Update"/> call, see
/// <see cref="ParallelSystemScheduler"/>), not just once at <see cref="WorldBuilder.Build"/>
/// time. Every entry here shares one component-access footprint pattern with no ordering
/// edges, since the conflict-packing path is the dominant cost at scale.
/// </summary>
[MemoryDiagnoser]
public class StagePlannerBenchmarks
{
    private struct BenchComponentA : IComponent;
    private struct BenchComponentB : IComponent;
    private struct BenchComponentC : IComponent;

    private struct D0; private struct D1; private struct D2; private struct D3; private struct D4;
    private struct D5; private struct D6; private struct D7; private struct D8; private struct D9;

    // Nested private and closed over marker type parameters so the generator's
    // per-Type AddSystem<T>() emission skips it (mirrors the existing nested-class
    // exclusion) — this only needs to exist as a StagePlanner input, never registered
    // through the real AddSystem<T>() surface.
    private sealed class BenchSystem<THundreds, TTens, TOnes> : EcsSystem
    {
        protected override void Execute(World world, Time time) { }
    }

    private static readonly Type[] Digits =
        [typeof(D0), typeof(D1), typeof(D2), typeof(D3), typeof(D4), typeof(D5), typeof(D6), typeof(D7), typeof(D8), typeof(D9)];

    // ResolveAccess in StagePlanner keys its per-Type access lookup by the *instance's*
    // concrete Type, so distinct systems need genuinely distinct Types here — a single
    // reused BenchSystem class (as this benchmark previously used) collapses every
    // entry's resolved access down to whichever one was registered last, which quietly
    // turned this into an all-mutually-conflicting worst case (one system per stage)
    // rather than the realistic mixed-footprint scenario the benchmark is meant to model.
    private static Type DistinctSystemType(int i) =>
        typeof(BenchSystem<,,>).MakeGenericType(Digits[i / 100 % 10], Digits[i / 10 % 10], Digits[i % 10]);

    /// <summary>10/50/200: a small system list, a realistic mid-size game, and a deliberately large one — the point where a regression would first become visible.</summary>
    [Params(10, 50, 200)]
    public int SystemCount { get; set; }

    private List<SystemEntry> _entries = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Three component types, round-robin: every third system shares an access
        // footprint with (and so conflicts with) another, the rest pack freely — a rough
        // stand-in for a real game's mixed read/write graph, not a worst-case adversarial
        // one (e.g. all-conflicting, which would degrade to one system per stage).
        var componentTypes = new[] { typeof(BenchComponentA), typeof(BenchComponentB), typeof(BenchComponentC) };

        _entries = new List<SystemEntry>(SystemCount);
        for (var i = 0; i < SystemCount; i++)
        {
            var type = DistinctSystemType(i);
            var instance = (EcsSystem)Activator.CreateInstance(type)!;
            _entries.Add(new SystemEntry
            {
                SystemType = type,
                Construct = _ => instance,
                Access = new SystemAccess(Reads: [], Writes: [componentTypes[i % componentTypes.Length]]),
                Instance = instance,
            });
        }
    }

    [Benchmark]
    public IReadOnlyList<IReadOnlyList<EcsSystem>> BuildStages() => StagePlanner.BuildStages(_entries);
}
