using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

/// <summary>
/// Exercises QuerySystem&lt;T&gt; outside Wyrd.Ecs.Tests, so this project's own
/// analyzer/interceptor wiring (Wyrd.Ecs.SystemGenerators, Wyrd.Ecs.Analyzers,
/// Wyrd.Ecs.Interceptors, plus InterceptorsNamespaces) is proven to work for a
/// consuming project other than the one that originally built it.
/// </summary>
public sealed partial class PositionDriftSystem : QuerySystem<Position>
{
    protected override void Execute(World world, ulong tick, ref Position component0)
    {
        component0.X += component0.Y * 0f;
    }
}

[MemoryDiagnoser]
public class QuerySystemBenchmarks
{
    private const int EntityCount = 10_000;

    private World _world = null!;
    private PositionDriftSystem _system = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _world = new World();
        for (var i = 0; i < EntityCount; i++)
            _world.Commands.CreateEntity(new Position());
        _world.ApplyCommands();

        _system = new PositionDriftSystem();
    }

    [Benchmark]
    public void RunOnce()
    {
        _world.AdvanceTick();
        _system.RunOnce(_world, tick: 0);
    }
}
