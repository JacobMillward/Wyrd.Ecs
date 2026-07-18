using BenchmarkDotNet.Attributes;
using Friflo.Engine.ECS;

namespace FrifloBenchmarks;

[MemoryDiagnoser]
public class QueryIterationBenchmarks
{
    private const int EntityCount = 10_000;

    [Params(false, true)]
    public bool Fragmented { get; set; }

    // One EntityStore per arity — a lower-arity query matches any archetype that
    // contains at least its required components, so if arity 2's entities also
    // carried arity 1's components in a shared store, the arity-1 query would
    // silently match every arity's entities instead of just its own 10,000.
    private EntityStore _store1 = null!;
    private EntityStore _store2 = null!;
    private EntityStore _store3 = null!;
    private EntityStore _store4 = null!;
    private EntityStore _store5 = null!;
    private ArchetypeQuery<Position> _query1 = null!;
    private ArchetypeQuery<Position, Velocity> _query2 = null!;
    private ArchetypeQuery<Position, Velocity, Health> _query3 = null!;
    private ArchetypeQuery<Position, Velocity, Health, BulkPayload> _query4 = null!;
    private ArchetypeQuery<Position, Velocity, Health, BulkPayload, Padding1> _query5 = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _store1 = new EntityStore();
        _store2 = new EntityStore();
        _store3 = new EntityStore();
        _store4 = new EntityStore();
        _store5 = new EntityStore();

        for (var i = 0; i < EntityCount; i++)
        {
            var e1 = _store1.CreateEntity(new Position());
            var e2 = _store2.CreateEntity(new Position(), new Velocity());
            var e3 = _store3.CreateEntity(new Position(), new Velocity(), new Health());
            var e4 = _store4.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload());
            var e5 = _store5.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload(), new Padding1());

            if (Fragmented)
            {
                Fragmentation.AddFragTag(e1, i);
                Fragmentation.AddFragTag(e2, i);
                Fragmentation.AddFragTag(e3, i);
                Fragmentation.AddFragTag(e4, i);
                Fragmentation.AddFragTag(e5, i);
            }
        }

        _query1 = _store1.Query<Position>();
        _query2 = _store2.Query<Position, Velocity>();
        _query3 = _store3.Query<Position, Velocity, Health>();
        _query4 = _store4.Query<Position, Velocity, Health, BulkPayload>();
        _query5 = _store5.Query<Position, Velocity, Health, BulkPayload, Padding1>();
    }

    [Benchmark(Baseline = true)]
    public void OneComponent_ForEachEntity()
    {
        _query1.ForEachEntity((ref Position position, Entity _) =>
        {
            position.X += position.Y * 0f;
        });
    }

    [Benchmark]
    public void OneComponent_Chunks()
    {
        foreach (var (position, entities) in _query1.Chunks)
            for (var n = 0; n < entities.Length; n++)
                position[n].X += position[n].Y * 0f;
    }

    [Benchmark]
    public void TwoComponent_ForEachEntity()
    {
        _query2.ForEachEntity((ref Position position, ref Velocity velocity, Entity _) =>
        {
            position.X += velocity.X * 0f;
        });
    }

    [Benchmark]
    public void TwoComponent_Chunks()
    {
        foreach (var (position, velocity, entities) in _query2.Chunks)
            for (var n = 0; n < entities.Length; n++)
                position[n].X += velocity[n].X * 0f;
    }

    [Benchmark]
    public void ThreeComponent_Chunks()
    {
        foreach (var (position, velocity, health, entities) in _query3.Chunks)
            for (var n = 0; n < entities.Length; n++)
                health[n].Current += (position[n].X + velocity[n].X) * 0f;
    }

    [Benchmark]
    public void FourComponent_Chunks()
    {
        foreach (var (position, velocity, health, payload, entities) in _query4.Chunks)
            for (var n = 0; n < entities.Length; n++)
                health[n].Current += (position[n].X + velocity[n].X + payload[n].A) * 0f;
    }

    [Benchmark]
    public void FiveComponent_Chunks()
    {
        foreach (var (position, velocity, health, payload, padding, entities) in _query5.Chunks)
            for (var n = 0; n < entities.Length; n++)
                health[n].Current += (position[n].X + velocity[n].X + payload[n].A + padding[n].Value) * 0f;
    }
}
