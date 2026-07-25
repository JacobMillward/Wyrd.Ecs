using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;

namespace Wyrd.Ecs.Benchmarks.WyrdEcs;

[MemoryDiagnoser]
public class QueryIterationBenchmarks
{
    private const int EntityCount = 10_000;

    // Built once, mirroring how World.Query<TAccess0> itself (and, eventually,
    // generator-emitted code for the new unbounded query-shape design) caches one
    // ArchetypeQuery per shape in a static field and resolves it fresh
    // (cache-backed via World.GetMatchingArchetypes) on every call.
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

    /// <summary>
    /// Same access pattern as <see cref="OneComponent_ChunkCallback"/>, calling
    /// <see cref="ArchetypeQuery"/>/<see cref="ArchetypeChunk"/> directly instead of through
    /// the <see cref="ChunkAction{TAccess0}"/> delegate <c>World.Query&lt;TAccess0&gt;</c>
    /// wraps it in -- the same underlying implementation either way, so this measures
    /// wrapper overhead (delegate dispatch, closure capture), not a different code path.
    /// This is also the calling style generator-emitted code for the new unbounded
    /// query-shape design will use.
    /// </summary>
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

    /// <summary>
    /// Same access pattern as <see cref="TwoComponent_ChunkCallback"/>, calling
    /// <see cref="ArchetypeQuery"/>/<see cref="ArchetypeChunk"/> directly with the loop
    /// written bare-inline -- see <see cref="OneComponent_ArchetypeQuery"/>. Kept
    /// deliberately alongside <see cref="TwoComponent_ArchetypeQuery_LocalFunction"/>: with
    /// two or more live accessors, this bare-inline shape measures consistently slower
    /// (~25-40%) than either the delegate-wrapped or local-function versions, a JIT
    /// register-allocation artifact confirmed in `wrapper-vs-inline-spike/`, not something
    /// specific to this primitive -- documented here so the gap (and its fix, below) stays
    /// visible in the one place someone benchmarking this API would actually look.
    /// </summary>
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

    /// <summary>
    /// Same work as <see cref="TwoComponent_ArchetypeQuery"/>, with the per-chunk body
    /// factored into a <c>static</c> local function instead of written bare-inline in the
    /// loop. A direct call, not a delegate -- no indirection, no allocation -- but gives
    /// the JIT a small, dedicated method to allocate registers for, matching
    /// <see cref="TwoComponent_ChunkCallback"/>'s performance despite calling
    /// <see cref="ArchetypeChunk"/> directly. This is the recommended pattern for any
    /// multi-accessor loop written directly against <see cref="ArchetypeQuery"/>.
    /// </summary>
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
}
