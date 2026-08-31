---
title: Input
description: Bind keys and mouse buttons to your own action enum, and read resolved state every tick.
---

`Wyrd.Ecs.Input` resolves physical keys and mouse buttons into named actions you define, strongly typed as your own enum. `AddInput` needs `AddWindow` called somewhere in the same chain, it reads events off `PlatformSystem` - but not necessarily first, construction order doesn't matter.

## Declaring actions and binding them

```csharp
using Wyrd.Ecs.Input;
using SDL3;

public enum PlayerAction { Move, Jump }

var bindings = new BindingTable<PlayerAction>()
    .BindAxis2D(PlayerAction.Move, SDL.Scancode.W, SDL.Scancode.S, SDL.Scancode.A, SDL.Scancode.D)
    .Bind(PlayerAction.Jump, SDL.Scancode.Space);

var world = new WorldBuilder()
    .AddWindow("My Game", 1280, 720)
    .AddInput(bindings)
    .Build();
```

`BindAxis2D` composes four keys into one `Vector2`, each contributing +/-1 on its axis, clamped to unit length. `Bind` maps one or more keys or mouse buttons to a digital action. Both are additive across calls, `Unbind` removes a binding. An action is either digital or an axis, never both, binding it the other way throws until you `Unbind` first.

:::note
Every `Bind`/`BindAxis2D`/`Unbind` overload above targets profile 0 by default. A `profile` parameter is there for more than one player, see [Multi-Device](/build/input/multi-device/).
:::

## Reading resolved state

```csharp
public sealed partial class MovementSystem : QuerySystem
{
    [Resource] public IntentState<PlayerAction> Input { get; private set; }

    protected override IQuery DefineQuery(Query query) => query.With<Position>();

    public void Update(Time time, ref Position position)
    {
        if (Input.TryGet(PlayerAction.Move, out var move))
            position.X += move.Value.X * (float)time.Delta.TotalSeconds;
    }
}
```

`IntentState<TAction>` is a resource, `IntentSystem<TAction>` (registered by `AddInput`) republishes it fresh every tick. `ActionState` carries `IsHeld`, `JustPressed`, `JustReleased`, and `Value` (the resolved `Vector2`, `UnitX` for a held digital action, the composite for an axis). `TryGet` returns `false` for an action nothing ever bound, the indexer (`Input[PlayerAction.Move]`) throws instead, use whichever fits the call site.

### Reading edges from a fixed-timestep system

`JustPressed`/`JustReleased` are recomputed fresh on every real `world.Update()` call, safe from a system running at the default cadence but not a `[FixedTimestep]` one, a fixed step doesn't line up one-to-one with real calls, so it can miss an edge or double-count it. Use `TickJustPressed`/`TickJustReleased` instead, from a `[FixedTimestep]` system:

```csharp
[FixedTimestep]
public sealed partial class JumpSystem : QuerySystem
{
    [Resource] public IntentState<PlayerAction> Input { get; private set; }

    protected override IQuery DefineQuery(Query query) => query.With<Velocity>();

    public void Update(Time time, ref Velocity velocity)
    {
        if (Input[PlayerAction.Jump].TickJustPressed) velocity.Y = JumpSpeed;
    }
}
```

They accumulate across however many real calls land inside one fixed step, then clear exactly once per step, so a fast frame's burst of real calls before the next fixed step still registers the press.

:::caution
Reading `JustPressed`/`JustReleased` from a `[FixedTimestep]` system is a compile error (WYRD011), naming `TickJustPressed`/`TickJustReleased` as the fix.
:::

## Mouse

```csharp
Input.MousePosition;  // window coordinates, shared across every profile
Input.MouseDelta;     // this tick's movement, reset every tick
Input.WheelDelta;      // this tick's scroll, reset every tick
```

Read directly off `IntentState<TAction>`, not tied to any bound action.

## Next

[Audio](/build/audio/) for sound effects and music, [Multi-Device](/build/input/multi-device/) for more than one keyboard or mouse, [Remapping](/build/input/remapping/) for saving player-customized bindings.
