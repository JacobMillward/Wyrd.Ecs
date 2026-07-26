using BenchmarkDotNet.Attributes;
using Wyrd.Ecs;
using Comparison.Wyrd;

namespace Comparison.MultiQueryTick;

public partial class MultiQueryTickBenchmarks
{
    private sealed class WyrdContext
    {
        public readonly World World = new();

        public static readonly ArchetypeQuery PositionOnlyQuery = ArchetypeQuery.Empty.Access<Mut<Position>>();
        public static readonly ArchetypeQuery VelocityOnlyQuery = ArchetypeQuery.Empty.Access<Mut<Velocity>>();
        public static readonly ArchetypeQuery HealthOnlyQuery = ArchetypeQuery.Empty.Access<Mut<Health>>();

        public WyrdContext()
        {
            for (var i = 0; i < EntityCountPerQuery; i++)
                World.Commands.CreateEntity(new Position());

            for (var i = 0; i < EntityCountPerQuery; i++)
                World.Commands.CreateEntity(new Velocity());

            for (var i = 0; i < EntityCountPerQuery; i++)
                World.Commands.CreateEntity(new Health());

            World.ApplyCommands();
        }
    }

    [Context] private WyrdContext _wyrd = null!;

    [Benchmark(Baseline = true)]
    public void Wyrd_RunThreeQueriesOneTick()
    {
        foreach (var chunk in WyrdContext.PositionOnlyQuery.Resolve(_wyrd.World))
        {
            var position = chunk.Access<Mut<Position>>();
            for (var i = 0; i < chunk.Count; i++)
                position[i].X += position[i].Y * 0f;
        }

        foreach (var chunk in WyrdContext.VelocityOnlyQuery.Resolve(_wyrd.World))
        {
            var velocity = chunk.Access<Mut<Velocity>>();
            for (var i = 0; i < chunk.Count; i++)
                velocity[i].X += velocity[i].Y * 0f;
        }

        foreach (var chunk in WyrdContext.HealthOnlyQuery.Resolve(_wyrd.World))
        {
            var health = chunk.Access<Mut<Health>>();
            for (var i = 0; i < chunk.Count; i++)
                health[i].Current += health[i].Max * 0f;
        }
    }
}
