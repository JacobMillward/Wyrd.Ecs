using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Measures <see cref="World.ReadChanges{T}"/>'s full-archetype scan cost at the
/// engine's ~20,000-entity target scale.
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
            _world.Commands.CreateEntity(new Position());
        _world.ApplyCommands();
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
