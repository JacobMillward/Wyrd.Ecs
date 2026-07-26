using BenchmarkDotNet.Attributes;
using Friflo.Engine.ECS;
using Comparison.Friflo;
// Friflo.Engine.ECS ships its own built-in Position type, colliding with our vocabulary's —
// disambiguate in favor of ours everywhere in this file.
using Position = Comparison.Friflo.Position;

namespace Comparison.MultiQueryTick;

public partial class MultiQueryTickBenchmarks
{
    private sealed class FrifloContext
    {
        public readonly EntityStore Store = new();
        public readonly ArchetypeQuery<Position> PositionOnlyQuery;
        public readonly ArchetypeQuery<Velocity> VelocityOnlyQuery;
        public readonly ArchetypeQuery<Health> HealthOnlyQuery;

        public FrifloContext()
        {
            for (var i = 0; i < EntityCountPerQuery; i++)
                Store.CreateEntity(new Position());

            for (var i = 0; i < EntityCountPerQuery; i++)
                Store.CreateEntity(new Velocity());

            for (var i = 0; i < EntityCountPerQuery; i++)
                Store.CreateEntity(new Health());

            PositionOnlyQuery = Store.Query<Position>();
            VelocityOnlyQuery = Store.Query<Velocity>();
            HealthOnlyQuery = Store.Query<Health>();
        }
    }

    [Context] private FrifloContext _friflo = null!;

    [Benchmark]
    public void Friflo_RunThreeQueriesOneTick()
    {
        foreach (var (position, entities) in _friflo.PositionOnlyQuery.Chunks)
            for (var n = 0; n < entities.Length; n++)
                position[n].X += position[n].Y * 0f;

        foreach (var (velocity, entities) in _friflo.VelocityOnlyQuery.Chunks)
            for (var n = 0; n < entities.Length; n++)
                velocity[n].X += velocity[n].Y * 0f;

        foreach (var (health, entities) in _friflo.HealthOnlyQuery.Chunks)
            for (var n = 0; n < entities.Length; n++)
                health[n].Current += health[n].Max * 0f;
    }
}
