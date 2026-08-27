using Wyrd.Ecs.Input;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

public sealed class PauseSystem : EcsSystem
{
    protected override void Execute(World world, Time time)
    {
        if (world.GetSystem<GameOverSystem>().HasTriggered) return;

        var input = world.GetResourceRef<IntentState<GameAction>>();
        if (!input[GameAction.Pause].JustPressed) return;

        if (world.IsPaused) world.Resume();
        else world.Pause();
    }
}
