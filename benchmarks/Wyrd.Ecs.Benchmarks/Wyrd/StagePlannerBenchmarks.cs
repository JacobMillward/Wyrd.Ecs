using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Benchmarks <see cref="StagePlanner.BuildStages"/> at increasing system counts. This is
/// the recompute the system-management redesign now triggers on every runtime
/// <c>AddSystem</c>/<c>RemoveSystem</c> (deferred, coalesced to at most once per
/// <see cref="World.Update"/> call — see <see cref="ParallelSystemScheduler"/>), not just
/// once at <see cref="WorldBuilder.Build"/> time as before. Every entry here shares one
/// component-access footprint pattern (no ordering edges) — the conflict-packing path is
/// the dominant cost at scale; edge-heavy graphs are a candidate follow-up dimension, not
/// covered here.
/// </summary>
[MemoryDiagnoser]
public class StagePlannerBenchmarks
{
    private struct BenchComponentA : IComponent;
    private struct BenchComponentB : IComponent;
    private struct BenchComponentC : IComponent;

    private sealed class BenchSystem : EcsSystem
    {
        protected override void Execute(World world, Time time) { }
    }

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
            var instance = new BenchSystem();
            _entries.Add(new SystemEntry
            {
                SystemType = typeof(BenchSystem),
                Construct = _ => instance,
                Access = new SystemAccess(Reads: [], Writes: [componentTypes[i % componentTypes.Length]]),
                Instance = instance,
            });
        }
    }

    [Benchmark]
    public IReadOnlyList<IReadOnlyList<EcsSystem>> BuildStages() => StagePlanner.BuildStages(_entries);
}
