using SDL3;
using Wyrd.Ecs.Input;

namespace Wyrd.Ecs.Examples.Asteroids;

internal static class Bindings
{
    public static BindingTable<GameAction> Default() => new BindingTable<GameAction>()
        .Bind(GameAction.Thrust, SDL.Scancode.W, SDL.Scancode.Up)
        .Bind(GameAction.TurnLeft, SDL.Scancode.A, SDL.Scancode.Left)
        .Bind(GameAction.TurnRight, SDL.Scancode.D, SDL.Scancode.Right)
        .Bind(GameAction.Fire, SDL.Scancode.Space)
        .Bind(GameAction.Pause, SDL.Scancode.P)
        .Bind(GameAction.Save, SDL.Scancode.S)
        .Bind(GameAction.Load, SDL.Scancode.L)
        .Bind(GameAction.Reset, SDL.Scancode.R);
}
