using BenchmarkDotNet.Attributes;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

public struct FfePosition : IComponent { public float X, Y; }
public struct FfeVelocity : IComponent { public float X, Y; }
public struct FfeDead : ITag { }

/// <summary>
/// Measures the per-invocation overhead of generated fluent <c>.ForEach</c>: every call
/// probes the archetype-set cache with the backend's accessor terms paired against the
/// caller's chain filter. A typical chain (.With terms only) and a filtered chain
/// (.With + .Without) are each resolved ResolutionsPerInvocation times with a trivial body,
/// so the measurement isolates resolution cost from iteration cost. The sink defeats
/// dead-code elimination.
/// </summary>
[MemoryDiagnoser]
public class FluentForEachResolutionBenchmarks
{
    public const int EntityCount = 20_000;
    private const int ResolutionsPerInvocation = 1024;

    private World _world = null!;
    private int _sink;

    [GlobalSetup]
    public void Setup()
    {
        _world = new World();
        for (var i = 0; i < EntityCount; i++)
        {
            var e = _world.Commands.CreateEntity(new FfePosition(), new FfeVelocity());
            if (i % 8 == 0)
                _world.Commands.AddTag<FfeDead>(e);
            _ = e;
        }
        _world.ApplyCommands();
        _world.AdvanceTick();
    }

    /// <summary>The common shape: accessors plus .With terms, no exclusion.</summary>
    [Benchmark(Baseline = true)]
    public void ForEach_WithTerms()
    {
        for (var i = 0; i < ResolutionsPerInvocation; i++)
            _world.Query().With<FfePosition>().ForEach(0, (in int _, ref FfePosition p) => { _sink += p.Y > 0 ? 1 : 0; });
    }

    /// <summary>Accessors, .With and .Without terms.</summary>
    [Benchmark]
    public void ForEach_WithAndWithoutTerms()
    {
        for (var i = 0; i < ResolutionsPerInvocation; i++)
            _world.Query().With<FfePosition>().Without<FfeDead>().ForEach(0, (in int _, ref FfePosition p) => { _sink += p.Y > 0 ? 1 : 0; });
    }
}
