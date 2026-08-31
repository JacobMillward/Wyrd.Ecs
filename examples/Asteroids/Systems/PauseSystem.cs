using Wyrd.Ecs.Input;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

public sealed partial class PauseSystem : EcsSystem
{
    [Resource] public partial IntentState<GameAction> Input { get; }

    protected override void Execute(World world, Time time)
    {
        if (world.GetSystem<GameOverSystem>().HasTriggered) return;

        if (!Input[GameAction.Pause].JustPressed) return;

        if (world.IsPaused) world.Resume();
        else world.Pause();
    }
}
