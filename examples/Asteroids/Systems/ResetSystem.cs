using SDL3;
using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Input;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

/// <summary>Respawns the ship and asteroid field and zeroes the score, without a full process restart. Bound to R.</summary>
public sealed class ResetSystem : EcsSystem
{
    private readonly Random _rng = new();

    protected override void Execute(World world, Time time)
    {
        var input = world.GetResourceRef<IntentState<GameAction>>();
        if (!input[GameAction.Reset].JustPressed) return;

        world.Query().Has<Bullet>().ForEach((EntityView entity) => entity.DestroyEntity());
        world.Query().Has<Asteroid>().ForEach((EntityView entity) => entity.DestroyEntity());

        world.Query().With<Transform, Velocity, Ship>().ForEach((ref Transform transform, ref Velocity velocity, ref Ship ship) =>
        {
            transform = Transform.Identity;
            velocity = default;
            ship.Heading = default;
        });

        world.Query().With<Score>().ForEach((ref Score score) => score.Value = 0);

        var assets = world.GetResourceRef<GameAssets>();
        AsteroidSpawner.SpawnInitialWave(world.Commands, assets.AsteroidTemplate, _rng);

        world.GetSystem<GameOverSystem>().Reset();
        world.TimeScale = 1.0;
        world.Resume();

        SDL.SetWindowTitle(world.GetSystem<PlatformSystem>().Window, "Asteroids - Score 0");
    }
}
