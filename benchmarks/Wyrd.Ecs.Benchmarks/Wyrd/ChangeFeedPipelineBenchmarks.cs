using BenchmarkDotNet.Attributes;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

public struct FeedPosition : IComponent { public float X, Y; }

/// <summary>
/// Measures the <see cref="World.Subscribe{T}"/> change-feed pipeline end to end:
/// value-change scans fanned out at tick advance, subscriber drains, and structural
/// lifecycle events published inline during command application. EntityCount mirrors the
/// suite's ~20k target scale; structural batches are 1k create+destroy pairs per
/// invocation, self-resetting so invocations stay comparable. Drain sinks consume the
/// returned lists so nothing is dead-code eliminated.
/// </summary>
[MemoryDiagnoser]
public class ChangeFeedPipelineBenchmarks
{
    public const int EntityCount = 20_000;
    private const int StructuralPairCount = 1_000;

    private World _world = null!;
    private Entity[] _entities = null!;
    private ChangeSubscription _single = null!;
    private ChangeSubscription[] _four = null!;
    private ChangeSubscription _lifecycle = null!;

    [GlobalSetup]
    public void Setup()
    {
        _world = new World();
        _entities = new Entity[EntityCount];
        for (var i = 0; i < EntityCount; i++)
            _entities[i] = _world.Commands.CreateEntity(new FeedPosition());
        _world.ApplyCommands();

        _single = _world.Subscribe<FeedPosition>();
        _four =
        [
            _world.Subscribe<FeedPosition>(),
            _world.Subscribe<FeedPosition>(),
            _world.Subscribe<FeedPosition>(),
            _world.Subscribe<FeedPosition>(),
        ];
        _lifecycle = _world.SubscribeEntityLifecycle();
    }

    /// <summary>One subscriber, every row touched: scan + publish fan-out + drain, the full per-tick pipeline.</summary>
    [Benchmark(Baseline = true)]
    public int TickScan_AllChanged_SingleSubscriber()
    {
        for (var i = 0; i < EntityCount; i++)
            _world.GetComponent<FeedPosition>(_entities[i]).X++;

        _world.AdvanceTick();
        return _single.Drain().Count;
    }

    /// <summary>Same shape with four subscribers of the same type: one shared scan, four-way fan-out.</summary>
    [Benchmark]
    public int TickScan_AllChanged_FourSubscribers()
    {
        for (var i = 0; i < EntityCount; i++)
            _world.GetComponent<FeedPosition>(_entities[i]).X--;

        _world.AdvanceTick();
        var total = 0;
        foreach (var subscription in _four)
            total += subscription.Drain().Count;
        return total;
    }

    /// <summary>Structural events published inline while commands apply: create+destroy pairs each invocation.</summary>
    [Benchmark]
    public int StructuralEvents_CreateDestroyPairs()
    {
        var spawned = new Entity[StructuralPairCount];
        for (var i = 0; i < StructuralPairCount; i++)
            spawned[i] = _world.Commands.CreateEntity(new FeedPosition());
        _world.ApplyCommands();

        foreach (var entity in spawned)
            _world.Commands.DestroyEntity(entity);
        _world.ApplyCommands();

        // Both lifecycle entries per pair land in the single lifecycle drain.
        return _lifecycle.Drain().Count;
    }
}
