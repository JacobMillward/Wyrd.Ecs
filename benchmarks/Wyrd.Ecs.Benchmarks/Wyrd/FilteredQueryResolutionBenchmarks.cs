using BenchmarkDotNet.Attributes;
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

public struct FiltHealth : IComponent { public float Value; }
public struct FiltPoisoned : ITag { }
public struct FiltBurning : ITag { }

/// <summary>
/// Measures archetype-filter query resolution: the dictionary probe every filtered
/// <c>.ForEach</c> pays per invocation (filter hash + key equality), and the miss path's
/// per-archetype matching. Warm hits are the steady-state frame cost; the cold-miss shape
/// exercises <c>Matches</c> across a fragmented archetype set. Resolution is measured
/// directly via the internal API so filter costs aren't drowned by iteration.
/// </summary>
[MemoryDiagnoser]
public class FilteredQueryResolutionBenchmarks
{
    private World _world = null!;
    private TypeBitSet _required = default!;
    private ArchetypeFilter _filter;

    [GlobalSetup]
    public void Setup()
    {
        _world = new World();
        // Fragmented signature space so a cold resolution has real matching work.
        for (var i = 0; i < 64; i++)
        {
            var e = _world.Commands.CreateEntity(new FiltHealth());
            switch (i % 3)
            {
                case 0: _world.Commands.AddTag<FiltPoisoned>(e); break;
                case 1: _world.Commands.AddTag<FiltBurning>(e); break;
            }
        }
        _world.ApplyCommands();

        _required = TypeBitSet.Empty.With(TypeIndex<FiltHealth>.Value);
        _filter = ArchetypeFilter.Empty.Without<FiltPoisoned>().Any<FiltPoisoned, FiltBurning>();
    }

    private const int ResolutionsPerInvocation = 4096;

    /// <summary>Steady state: the cache hit path, paid once per filtered query per frame.</summary>
    [Benchmark(Baseline = true)]
    public int WarmResolution()
    {
        var sink = 0;
        for (var i = 0; i < ResolutionsPerInvocation; i++)
            sink += _world.GetMatchingArchetypes(_required, _filter).Length;
        return sink;
    }

    /// <summary>Cold: invalidation forces the full scan with per-archetype Matches evaluation.</summary>
    [Benchmark]
    public int ColdResolution()
    {
        var sink = 0;
        for (var i = 0; i < 64; i++)
        {
            // A brand-new archetype each round clears both caches; the next lookup rebuilds.
            _ = _world.Commands.CreateEntity(new FiltHealth());
            _world.ApplyCommands();
            sink += _world.GetMatchingArchetypes(_required, _filter).Length;
        }

        return sink;
    }
}
