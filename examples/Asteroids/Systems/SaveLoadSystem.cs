using SDL3;
using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Input;
using Wyrd.Ecs.Persistence;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

public sealed partial class SaveLoadSystem : EcsSystem
{
    [Resource] public partial IntentState<GameAction> Input { get; }

    protected override void Execute(World world, Time time)
    {
        if (Input[GameAction.Save].JustPressed) world.Save();

        if (Input[GameAction.Load].JustPressed)
        {
            world.Load();
            world.GetSystem<GameOverSystem>().Reset();
            world.TimeScale = 1.0;
            world.Resume();

            // The title bar is the game's only score readout. ScoreSystem only refreshes it on
            // AsteroidDestroyed events, and Load doesn't emit any, so without this it would keep
            // showing whatever it said pre-load (a stale score, or stale "Game Over" text).
            var score = 0;
            world.Query().With<Score>().ForEach((in Score s) => score = s.Value);
            SDL.SetWindowTitle(world.GetSystem<PlatformSystem>().Window, $"Asteroids - Score {score}");
        }
    }
}
