using BenchmarkDotNet.Attributes;
using fennecs;
using Comparison.Fennecs;

namespace Comparison.MultiQueryTick;

public partial class MultiQueryTickBenchmarks
{
    private sealed class FennecsContext : IDisposable
    {
        public readonly World World = new();
        public readonly Stream<Position> PositionOnlyStream;
        public readonly Stream<Velocity> VelocityOnlyStream;
        public readonly Stream<Health> HealthOnlyStream;

        public FennecsContext()
        {
            for (var i = 0; i < EntityCountPerQuery; i++)
                World.Spawn().Add(new Position());

            for (var i = 0; i < EntityCountPerQuery; i++)
                World.Spawn().Add(new Velocity());

            for (var i = 0; i < EntityCountPerQuery; i++)
                World.Spawn().Add(new Health());

            PositionOnlyStream = World.Query<Position>().Stream();
            VelocityOnlyStream = World.Query<Velocity>().Stream();
            HealthOnlyStream = World.Query<Health>().Stream();
        }

        public void Dispose()
        {
            PositionOnlyStream.Query.Dispose();
            VelocityOnlyStream.Query.Dispose();
            HealthOnlyStream.Query.Dispose();
        }
    }

    [Context] private FennecsContext _fennecs = null!;

    [Benchmark]
    public void Fennecs_RunThreeQueriesOneTick()
    {
        _fennecs.PositionOnlyStream.For((ref Position position) => position.X += position.Y * 0f);
        _fennecs.VelocityOnlyStream.For((ref Velocity velocity) => velocity.X += velocity.Y * 0f);
        _fennecs.HealthOnlyStream.For((ref Health health) => health.Current += health.Max * 0f);
    }
}
