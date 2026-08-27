using Wyrd.Ecs.Input;
using Wyrd.Ecs.Persistence;

namespace Wyrd.Ecs.Examples.Asteroids.Systems;

public sealed class SaveLoadSystem : EcsSystem
{
    protected override void Execute(World world, Time time)
    {
        var input = world.GetResourceRef<IntentState<GameAction>>();

        if (input[GameAction.Save].JustPressed) world.Save();

        if (input[GameAction.Load].JustPressed)
        {
            world.Load();
            world.GetSystem<GameOverSystem>().Reset();
            world.TimeScale = 1.0;
            world.Resume();
        }
    }
}
