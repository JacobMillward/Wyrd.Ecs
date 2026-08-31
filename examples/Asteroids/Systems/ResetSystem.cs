using SDL3;
using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Input;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

/// <summary>Respawns the ship and asteroid field and zeroes the score, without a full process restart. Bound to R.</summary>
public sealed partial class ResetSystem : EcsSystem
{
    [Resource] public partial IntentState<GameAction> Input { get; }
    [Resource] public partial GameAssets Assets { get; }

    private readonly Random _rng = new();

    protected override void Execute(World world, Time time)
    {
        if (!Input[GameAction.Reset].JustPressed) return;

        world.Query().Has<Bullet>().ForEach((EntityView entity) => entity.DestroyEntity());
        world.Query().Has<Asteroid>().ForEach((EntityView entity) => entity.DestroyEntity());
        // The ship may already be gone (collision) or still alive (R pressed mid-run).
        // Destroy-then-recreate covers both without needing to distinguish them.
        world.Query().Has<Ship>().ForEach((EntityView entity) => entity.DestroyEntity());

        world.Query().With<Score>().ForEach((ref Score score) => score.Value = 0);

        world.Commands.CreateEntity(Assets.ShipTemplate);
        AsteroidSpawner.SpawnInitialWave(world.Commands, Assets.AsteroidTemplate, _rng);

        world.GetSystem<GameOverSystem>().Reset();
        world.TimeScale = 1.0;
        world.Resume();

        SDL.SetWindowTitle(world.GetSystem<PlatformSystem>().Window, "Asteroids - Score 0");
    }
}
