using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

/// <summary>
/// Measures AdvanceTick's per-tick retention-scan cost against a tracked component with
/// a large, persistently-unread backlog behind a consumer that only trickles forward by
/// one tick's worth per call — the shape a batched consumer (persistence, a network sync
/// pass) produces when it reads and advances on its own cadence rather than every tick.
/// EntitiesPerTick entities are touched every invocation (replenishing the backlog by
/// one tick's worth) and the consumer advances by exactly one tick's worth too, so the
/// live backlog size (BacklogTicks * EntitiesPerTick entries) stays constant across the
/// whole job instead of draining or growing without bound — isolating retention's
/// per-tick cost at a fixed, large backlog size rather than measuring log growth.
/// </summary>
[MemoryDiagnoser]
public class RetentionBenchmarks
{
    private const int BacklogTicks = 200;

    [Params(1_000, 5_000)]
    public int EntitiesPerTick { get; set; }

    private World _trackedWorld = null!;
    private World _untrackedWorld = null!;
    private Entity[] _entities = null!;
    private ChangeConsumer<Position> _consumer = null!;
    private int _consumerTick;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _untrackedWorld = new World();

        _trackedWorld = new World();
        _entities = new Entity[EntitiesPerTick];
        for (var i = 0; i < EntitiesPerTick; i++)
        {
            _entities[i] = _trackedWorld.CreateEntity();
            _trackedWorld.AddComponent<Position>(_entities[i]);
        }

        _consumer = _trackedWorld.RegisterChangeConsumer<Position>();
        _consumerTick = _trackedWorld.CurrentTick;

        for (var t = 0; t < BacklogTicks; t++)
        {
            _trackedWorld.AdvanceTick();
            TouchAll();
        }
    }

    private void TouchAll()
    {
        foreach (var entity in _entities)
            _trackedWorld.GetComponent<Position>(entity).X += 1f;
    }

    /// <summary>Retention's per-tick baseline cost when nothing is tracked at all.</summary>
    [Benchmark(Baseline = true)]
    public void AdvanceTick_NoTracking() => _untrackedWorld.AdvanceTick();

    /// <summary>
    /// One steady-state tick against a fixed-size, large unread backlog: touch every
    /// entity (appending EntitiesPerTick fresh entries), then let the consumer advance
    /// by exactly one tick's worth. Net backlog size neither drains nor grows across the
    /// run, isolating retention's per-tick cost at a constant, large backlog size.
    /// </summary>
    [Benchmark]
    public void AdvanceTick_SteadyStateBacklog()
    {
        _trackedWorld.AdvanceTick();
        TouchAll();
        _consumer.Advance(++_consumerTick);
    }
}
