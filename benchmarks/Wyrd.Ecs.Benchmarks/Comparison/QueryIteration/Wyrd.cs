using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Comparison.QueryIteration;

public partial class QueryIterationBenchmarks
{
    private sealed class WyrdContext
    {
        public readonly World World1 = new();
        public readonly World World2 = new();
        public readonly World World3 = new();
        public readonly World World4 = new();
        public readonly World World5 = new();

        public static readonly ArchetypeQuery Query1 = ArchetypeQuery.Empty.Access<Mut<Position>>();
        public static readonly ArchetypeQuery Query2 =
            ArchetypeQuery.Empty.Access<Mut<Position>>().Access<Ref<Velocity>>();
        public static readonly ArchetypeQuery Query3 = ArchetypeQuery.Empty
            .Access<Ref<Position>>().Access<Ref<Velocity>>().Access<Mut<Health>>();
        public static readonly ArchetypeQuery Query4 = ArchetypeQuery.Empty
            .Access<Ref<Position>>().Access<Ref<Velocity>>().Access<Mut<Health>>().Access<Ref<BulkPayload>>();
        public static readonly ArchetypeQuery Query5 = ArchetypeQuery.Empty
            .Access<Ref<Position>>().Access<Ref<Velocity>>().Access<Mut<Health>>()
            .Access<Ref<BulkPayload>>().Access<Ref<Padding1>>();

        public WyrdContext(int entityCount, bool fragmented)
        {
            for (var i = 0; i < entityCount; i++)
            {
                var e1 = World1.Commands.CreateEntity(new Position()).Entity;
                var e2 = World2.Commands.CreateEntity(new Position(), new Velocity()).Entity;
                var e3 = World3.Commands.CreateEntity(new Position(), new Velocity(), new Health()).Entity;
                var e4 = World4.Commands.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload()).Entity;
                var e5 = World5.Commands.CreateEntity(new Position(), new Velocity(), new Health(), new BulkPayload(), new Padding1()).Entity;

                if (fragmented)
                {
                    Fragmentation.AddFragTag(World1, e1, i);
                    Fragmentation.AddFragTag(World2, e2, i);
                    Fragmentation.AddFragTag(World3, e3, i);
                    Fragmentation.AddFragTag(World4, e4, i);
                    Fragmentation.AddFragTag(World5, e5, i);
                }
            }

            World1.ApplyCommands();
            World2.ApplyCommands();
            World3.ApplyCommands();
            World4.ApplyCommands();
            World5.ApplyCommands();
        }
    }

    [Context] private WyrdContext _wyrd = null!;

    [Benchmark(Baseline = true)]
    public void Wyrd_OneComponent_ArchetypeQuery()
    {
        foreach (var chunk in WyrdContext.Query1.Resolve(_wyrd.World1))
        {
            var position = chunk.Access<Mut<Position>>();
            for (var i = 0; i < chunk.Count; i++)
                position[i].X += position[i].Y * 0f;
        }
    }

    [Benchmark]
    public void Wyrd_OneComponent_FluentChain()
    {
        _wyrd.World1.Query().With<Position>()
            .ForEach(0, (in int _, ref Position p) => p.X += p.Y * 0f);
    }

    [Benchmark]
    public void Wyrd_TwoComponent_ArchetypeQuery()
    {
        foreach (var chunk in WyrdContext.Query2.Resolve(_wyrd.World2))
        {
            var position = chunk.Access<Mut<Position>>();
            var velocity = chunk.Access<Ref<Velocity>>();
            for (var i = 0; i < chunk.Count; i++)
                position[i].X += velocity[i].X * 0f;
        }
    }

    [Benchmark]
    public void Wyrd_TwoComponent_FluentChain()
    {
        _wyrd.World2.Query().With<Position>().With<Velocity>()
            .ForEach(0, (in int _, ref Position p, in Velocity v) => p.X += v.X * 0f);
    }

    [Benchmark]
    public void Wyrd_ThreeComponent_ArchetypeQuery()
    {
        foreach (var chunk in WyrdContext.Query3.Resolve(_wyrd.World3))
        {
            var position = chunk.Access<Ref<Position>>();
            var velocity = chunk.Access<Ref<Velocity>>();
            var health = chunk.Access<Mut<Health>>();
            for (var i = 0; i < chunk.Count; i++)
                health[i].Current += (position[i].X + velocity[i].X) * 0f;
        }
    }

    [Benchmark]
    public void Wyrd_ThreeComponent_FluentChain()
    {
        _wyrd.World3.Query().With<Position>().With<Velocity>().With<Health>()
            .ForEach(0, (in int _, in Position p, in Velocity v, ref Health h) => h.Current += (p.X + v.X) * 0f);
    }

    [Benchmark]
    public void Wyrd_FourComponent_ArchetypeQuery()
    {
        foreach (var chunk in WyrdContext.Query4.Resolve(_wyrd.World4))
        {
            var position = chunk.Access<Ref<Position>>();
            var velocity = chunk.Access<Ref<Velocity>>();
            var health = chunk.Access<Mut<Health>>();
            var payload = chunk.Access<Ref<BulkPayload>>();
            for (var i = 0; i < chunk.Count; i++)
                health[i].Current += (position[i].X + velocity[i].X + payload[i].A) * 0f;
        }
    }

    [Benchmark]
    public void Wyrd_FourComponent_FluentChain()
    {
        _wyrd.World4.Query().With<Position>().With<Velocity>().With<Health>().With<BulkPayload>()
            .ForEach(0, (in int _, in Position p, in Velocity v, ref Health h, in BulkPayload b) => h.Current += (p.X + v.X + b.A) * 0f);
    }

    [Benchmark]
    public void Wyrd_FiveComponent_ArchetypeQuery()
    {
        foreach (var chunk in WyrdContext.Query5.Resolve(_wyrd.World5))
        {
            var position = chunk.Access<Ref<Position>>();
            var velocity = chunk.Access<Ref<Velocity>>();
            var health = chunk.Access<Mut<Health>>();
            var payload = chunk.Access<Ref<BulkPayload>>();
            var padding = chunk.Access<Ref<Padding1>>();
            for (var i = 0; i < chunk.Count; i++)
                health[i].Current += (position[i].X + velocity[i].X + payload[i].A + padding[i].Value) * 0f;
        }
    }

    [Benchmark]
    public void Wyrd_FiveComponent_FluentChain()
    {
        _wyrd.World5.Query().With<Position>().With<Velocity>().With<Health>()
            .With<BulkPayload>().With<Padding1>()
            .ForEach(0, (in int _, in Position p, in Velocity v, ref Health h, in BulkPayload b, in Padding1 pad) =>
                h.Current += (p.X + v.X + b.A + pad.Value) * 0f);
    }
}
