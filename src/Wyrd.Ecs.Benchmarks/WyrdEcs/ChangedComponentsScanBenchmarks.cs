using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

/// <summary>
/// Measures <see cref="IWorld.ReadChanges{T}"/>'s full-archetype scan cost at the
/// ~20,000-entity scale validated in
/// <c>docs/superpowers/specs/2026-07-19-persistence-primitives-design.md</c> — the
/// number that decided tick-stamp-and-scan over an append log for this project's actual
/// consumer shape (one background persistence reader, not many independently-lagging
/// ones). Kept in the suite as the direct replacement for the retention-cost benchmark
/// this design removes: that measured trim cost against a growing backlog, which no
/// longer exists; this measures the cost the backlog existed to avoid, confirming it
/// stays cheap without it.
/// </summary>
[MemoryDiagnoser]
public class ChangedComponentsScanBenchmarks
{
    [Params(1_000, 20_000)]
    public int EntityCount { get; set; }

    private World _world = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _world = new World();
        _world.TrackChanges<Position>();

        for (var i = 0; i < EntityCount; i++)
        {
            var entity = _world.CreateEntity();
            _world.AddComponent<Position>(entity);
        }
    }

    /// <summary>
    /// The worst case for scan cost: every row is visited and none match, since the
    /// watermark is the current tick. A real caller reading on a cadence behind the
    /// simulation would see some matches, which costs no more per row than this does.
    /// </summary>
    [Benchmark]
    public int ReadChanges_NothingChangedSinceNow()
    {
        var count = 0;
        foreach (var change in _world.ReadChanges<Position>(_world.CurrentTick))
            count++;
        return count;
    }
}
