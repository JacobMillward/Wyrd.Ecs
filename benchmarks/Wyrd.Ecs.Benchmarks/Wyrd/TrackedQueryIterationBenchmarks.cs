using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// The <see cref="Tracked"/> dimension, Wyrd.Ecs-only with no Friflo or fennecs equivalent — see
/// <see cref="TrackedEntityLifecycleBenchmarks"/> for the same reasoning. Stays at arity 1-2 (the
/// scope the original implementation covered before the arity cap was removed) rather than
/// growing to 5 — the point of this class is measuring the tracked-write-stamping cost specifically,
/// not re-deriving the arity-scaling numbers the untracked
/// <see cref="Comparison.QueryIteration.QueryIterationBenchmarks"/> already covers.
/// </summary>
[MemoryDiagnoser]
public class TrackedQueryIterationBenchmarks
{
    private const int EntityCount = 10_000;

    private static readonly ArchetypeQuery OneComponentQuery = ArchetypeQuery.Empty.Access<Mut<Position>>();
    private static readonly ArchetypeQuery TwoComponentQuery = ArchetypeQuery.Empty.Access<Mut<Position>>().Access<Ref<Velocity>>();

    [Params(false, true)]
    public bool Fragmented { get; set; }

    [Params(false, true)]
    public bool Tracked { get; set; }

    private World _world1 = null!;
    private World _world2 = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _world1 = new World();
        _world2 = new World();

        if (Tracked)
        {
            _world1.TrackChanges<Position>();
            _world2.TrackChanges<Position>();
            _world2.TrackChanges<Velocity>();
        }

        for (var i = 0; i < EntityCount; i++)
        {
            var e1 = _world1.Commands.CreateEntity(new Position());
            var e2 = _world2.Commands.CreateEntity(new Position(), new Velocity());

            if (Fragmented)
            {
                Fragmentation.AddFragTag(_world1, e1, i);
                Fragmentation.AddFragTag(_world2, e2, i);
            }
        }
        _world1.ApplyCommands();
        _world2.ApplyCommands();
    }

    [Benchmark(Baseline = true)]
    public void OneComponent_ChunkCallback()
    {
        _world1.AdvanceTick();
        _world1.Query<Mut<Position>>(chunk =>
        {
            for (var i = 0; i < chunk.Length; i++)
                chunk[i].X += chunk[i].Y * 0f;
        });
    }

    [Benchmark]
    public void TwoComponent_ChunkCallback()
    {
        _world2.AdvanceTick();
        _world2.Query<Mut<Position>, Ref<Velocity>>((position, velocity) =>
        {
            for (var i = 0; i < position.Length; i++)
                position[i].X += velocity[i].X * 0f;
        });
    }

    [Benchmark]
    public void OneComponent_ArchetypeQuery()
    {
        _world1.AdvanceTick();
        foreach (var chunk in OneComponentQuery.Resolve(_world1))
        {
            var position = chunk.Access<Mut<Position>>();
            for (var i = 0; i < chunk.Count; i++)
                position[i].X += position[i].Y * 0f;
        }
    }

    [Benchmark]
    public void TwoComponent_ArchetypeQuery()
    {
        _world2.AdvanceTick();
        foreach (var chunk in TwoComponentQuery.Resolve(_world2))
        {
            var position = chunk.Access<Mut<Position>>();
            var velocity = chunk.Access<Ref<Velocity>>();
            for (var i = 0; i < chunk.Count; i++)
                position[i].X += velocity[i].X * 0f;
        }
    }

    [Benchmark]
    public void TwoComponent_ArchetypeQuery_LocalFunction()
    {
        _world2.AdvanceTick();
        foreach (var chunk in TwoComponentQuery.Resolve(_world2))
            Process(chunk.Access<Mut<Position>>(), chunk.Access<Ref<Velocity>>());

        static void Process(Mut<Position> position, Ref<Velocity> velocity)
        {
            for (var i = 0; i < position.Length; i++)
                position[i].X += velocity[i].X * 0f;
        }
    }

    [Benchmark]
    public void OneComponent_FluentChain()
    {
        _world1.AdvanceTick();
        _world1.Query().With<Position>()
            .ForEach(0, (in int _, ref Position p) => p.X += p.Y * 0f);
    }

    [Benchmark]
    public void TwoComponent_FluentChain()
    {
        _world2.AdvanceTick();
        _world2.Query().With<Position>().With<Velocity>()
            .ForEach(0, (in int _, ref Position p, in Velocity v) => p.X += v.X * 0f);
    }
}
