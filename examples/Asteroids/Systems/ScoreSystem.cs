using SDL3;
using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Examples.Asteroids.Events;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

public sealed partial class ScoreSystem(World world) : QuerySystem
{
    private readonly EventReader<AsteroidDestroyed> _destroyed = world.CreateEventReader<AsteroidDestroyed>();

    protected override IQuery DefineQuery(Query query) => query.With<Score>().Has<Game>();

    public void Update(Time time, World world, ref Score score)
    {
        var before = score.Value;
        foreach (var e in _destroyed.Read()) score.Value += e.Size.Points();
        if (score.Value == before) return;

        SDL.SetWindowTitle(world.GetSystem<PlatformSystem>().Window, $"Asteroids - Score {score.Value}");
    }
}
