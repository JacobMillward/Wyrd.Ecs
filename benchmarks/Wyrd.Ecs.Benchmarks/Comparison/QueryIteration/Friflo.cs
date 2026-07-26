using BenchmarkDotNet.Attributes;
using Friflo.Engine.ECS;
using Comparison.Friflo;
// Friflo.Engine.ECS ships its own built-in Position type, colliding with our vocabulary's —
// disambiguate in favor of ours everywhere in this file.
using Position = Comparison.Friflo.Position;

namespace Comparison.QueryIteration;

public partial class QueryIterationBenchmarks
{
    private sealed class FrifloContext
    {
        public readonly EntityStore Store1 = new();
        public readonly EntityStore Store2 = new();
        public readonly EntityStore Store3 = new();
        public readonly EntityStore Store4 = new();
        public readonly EntityStore Store5 = new();
        public readonly ArchetypeQuery<Position> Query1;
        public readonly ArchetypeQuery<Position, Velocity> Query2;
        public readonly ArchetypeQuery<Position, Velocity, Health> Query3;
        public readonly ArchetypeQuery<Position, Velocity, Health, BulkPayload> Query4;
        public readonly ArchetypeQuery<Position, Velocity, Health, BulkPayload, Padding1> Query5;

        public FrifloContext(int entityCount, bool fragmented)
        {
            for (var i = 0; i < entityCount; i++)
            {
                var e1 = Store1.CreateEntity(new Position());
                var e2 = Store2.CreateEntity(new Position(), new Velocity());
                var e3 = Store3.CreateEntity(new Position(), new Velocity(), new Health());
                var e4 = Store4.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload());
                var e5 = Store5.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload(), new Padding1());

                if (fragmented)
                {
                    Fragmentation.AddFragTag(e1, i);
                    Fragmentation.AddFragTag(e2, i);
                    Fragmentation.AddFragTag(e3, i);
                    Fragmentation.AddFragTag(e4, i);
                    Fragmentation.AddFragTag(e5, i);
                }
            }

            Query1 = Store1.Query<Position>();
            Query2 = Store2.Query<Position, Velocity>();
            Query3 = Store3.Query<Position, Velocity, Health>();
            Query4 = Store4.Query<Position, Velocity, Health, BulkPayload>();
            Query5 = Store5.Query<Position, Velocity, Health, BulkPayload, Padding1>();
        }
    }

    [Context] private FrifloContext _friflo = null!;

    [Benchmark]
    public void Friflo_OneComponent_ForEachEntity()
    {
        _friflo.Query1.ForEachEntity((ref Position position, Entity _) =>
        {
            position.X += position.Y * 0f;
        });
    }

    [Benchmark]
    public void Friflo_OneComponent_Chunks()
    {
        foreach (var (position, entities) in _friflo.Query1.Chunks)
            for (var n = 0; n < entities.Length; n++)
                position[n].X += position[n].Y * 0f;
    }

    [Benchmark]
    public void Friflo_TwoComponent_ForEachEntity()
    {
        _friflo.Query2.ForEachEntity((ref Position position, ref Velocity velocity, Entity _) =>
        {
            position.X += velocity.X * 0f;
        });
    }

    [Benchmark]
    public void Friflo_TwoComponent_Chunks()
    {
        foreach (var (position, velocity, entities) in _friflo.Query2.Chunks)
            for (var n = 0; n < entities.Length; n++)
                position[n].X += velocity[n].X * 0f;
    }

    [Benchmark]
    public void Friflo_ThreeComponent_Chunks()
    {
        foreach (var (position, velocity, health, entities) in _friflo.Query3.Chunks)
            for (var n = 0; n < entities.Length; n++)
                health[n].Current += (position[n].X + velocity[n].X) * 0f;
    }

    [Benchmark]
    public void Friflo_FourComponent_Chunks()
    {
        foreach (var (position, velocity, health, payload, entities) in _friflo.Query4.Chunks)
            for (var n = 0; n < entities.Length; n++)
                health[n].Current += (position[n].X + velocity[n].X + payload[n].A) * 0f;
    }

    [Benchmark]
    public void Friflo_FiveComponent_Chunks()
    {
        foreach (var (position, velocity, health, payload, padding, entities) in _friflo.Query5.Chunks)
            for (var n = 0; n < entities.Length; n++)
                health[n].Current += (position[n].X + velocity[n].X + payload[n].A + padding[n].Value) * 0f;
    }
}
