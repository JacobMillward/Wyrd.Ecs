using BenchmarkDotNet.Attributes;
using fennecs;
using Comparison.Fennecs;

namespace Comparison.QueryIteration;

public partial class QueryIterationBenchmarks
{
    private sealed class FennecsContext : IDisposable
    {
        public readonly World World1 = new();
        public readonly World World2 = new();
        public readonly World World3 = new();
        public readonly World World4 = new();
        public readonly World World5 = new();

        public readonly Stream<Position> Stream1;
        public readonly Stream<Position, Velocity> Stream2;
        public readonly Stream<Position, Velocity, Health> Stream3;
        public readonly Stream<Position, Velocity, Health, BulkPayload> Stream4;
        public readonly Stream<Position, Velocity, Health, BulkPayload, Padding1> Stream5;

        public FennecsContext(int entityCount, bool fragmented)
        {
            for (var i = 0; i < entityCount; i++)
            {
                var e1 = World1.Spawn().Add(new Position());
                var e2 = World2.Spawn().Add(new Position()).Add(new Velocity());
                var e3 = World3.Spawn().Add(new Position()).Add(new Velocity()).Add(new Health());
                var e4 = World4.Spawn().Add(new Position()).Add(new Velocity()).Add(new Health()).Add(new BulkPayload());
                var e5 = World5.Spawn().Add(new Position()).Add(new Velocity()).Add(new Health()).Add(new BulkPayload()).Add(new Padding1());

                if (fragmented)
                {
                    Fragmentation.AddFragTag(e1, i);
                    Fragmentation.AddFragTag(e2, i);
                    Fragmentation.AddFragTag(e3, i);
                    Fragmentation.AddFragTag(e4, i);
                    Fragmentation.AddFragTag(e5, i);
                }
            }

            Stream1 = World1.Query<Position>().Stream();
            Stream2 = World2.Query<Position, Velocity>().Stream();
            Stream3 = World3.Query<Position, Velocity, Health>().Stream();
            Stream4 = World4.Query<Position, Velocity, Health, BulkPayload>().Stream();
            Stream5 = World5.Query<Position, Velocity, Health, BulkPayload, Padding1>().Stream();
        }

        public void Dispose()
        {
            Stream1.Query.Dispose();
            Stream2.Query.Dispose();
            Stream3.Query.Dispose();
            Stream4.Query.Dispose();
            Stream5.Query.Dispose();
        }
    }

    [Context] private FennecsContext _fennecs = null!;

    [Benchmark]
    public void Fennecs_OneComponent_For()
    {
        _fennecs.Stream1.For((ref Position position) => position.X += position.Y * 0f);
    }

    [Benchmark]
    public void Fennecs_TwoComponent_For()
    {
        _fennecs.Stream2.For((ref Position position, ref Velocity velocity) => position.X += velocity.X * 0f);
    }

    [Benchmark]
    public void Fennecs_ThreeComponent_For()
    {
        _fennecs.Stream3.For((ref Position position, ref Velocity velocity, ref Health health) =>
            health.Current += (position.X + velocity.X) * 0f);
    }

    [Benchmark]
    public void Fennecs_FourComponent_For()
    {
        _fennecs.Stream4.For((ref Position position, ref Velocity velocity, ref Health health, ref BulkPayload payload) =>
            health.Current += (position.X + velocity.X + payload.A) * 0f);
    }

    [Benchmark]
    public void Fennecs_FiveComponent_For()
    {
        _fennecs.Stream5.For((ref Position position, ref Velocity velocity, ref Health health, ref BulkPayload payload, ref Padding1 padding) =>
            health.Current += (position.X + velocity.X + payload.A + padding.Value) * 0f);
    }
}
