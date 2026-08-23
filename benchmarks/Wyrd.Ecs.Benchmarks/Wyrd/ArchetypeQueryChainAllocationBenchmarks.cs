using BenchmarkDotNet.Attributes;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

public struct QchPosition : IComponent { public float X, Y; }
public struct QchVelocity : IComponent { public float X, Y; }
public struct QchDead : ITag { }

/// <summary>
/// Measures the per-call cost of building and resolving a hand-written
/// <see cref="ArchetypeQuery"/> chain: every filter-touching link (Access/Without) is
/// constructed fresh per iteration, the shape a <c>QuerySystem.DefineQuery</c> rebuild
/// pays on every Execute, resolved against a warm archetype-set cache. The prebuilt-chain
/// benchmark isolates steady-state cache-hit resolution for comparison.
/// </summary>
[MemoryDiagnoser]
public class ArchetypeQueryChainAllocationBenchmarks
{
    private const int ChainsPerInvocation = 1024;

    private World _world = null!;

    [GlobalSetup]
    public void Setup()
    {
        _world = new World();
        for (var i = 0; i < 256; i++)
        {
            var e = _world.Commands.CreateEntity(new QchPosition(), new QchVelocity());
            if (i % 4 == 0)
                _world.Commands.AddTag<QchDead>(e);
            _ = e;
        }
        _world.ApplyCommands();
    }

    /// <summary>A fresh Access + Without chain built and resolved every iteration.</summary>
    [Benchmark(Baseline = true)]
    public int FreshChain_ConstructAndResolve()
    {
        var sink = 0;
        for (var i = 0; i < ChainsPerInvocation; i++)
            sink += ArchetypeQuery.Empty
                .Access<Ref<QchPosition>>()
                .Without<QchDead>()
                .Resolve(_world)
                .Count;
        return sink;
    }

    /// <summary>The same-shaped query built once outside the loop: steady-state cache-hit resolution.</summary>
    [Benchmark]
    public int Prebuilt_Resolve()
    {
        var query = ArchetypeQuery.Empty.Access<Ref<QchPosition>>().Without<QchDead>();
        var sink = 0;
        for (var i = 0; i < ChainsPerInvocation; i++)
            sink += query.Resolve(_world).Count;
        return sink;
    }
}
