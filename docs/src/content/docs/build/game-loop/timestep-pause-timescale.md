---
title: Timestep, Pause & Timescale
description: Running a system at a fixed rate, freezing time, and speeding it up or down.
---

A platformer's physics wants a constant step no matter the frame rate. A pause menu wants to freeze the simulation without freezing the render loop. A slow-motion effect wants everything scaled down for a few seconds. All three are the same underlying clock, seen from a different angle.

## Fixed timestep

By default a system runs exactly once per `world.Update(...)` call, at whatever delta that call was given. Mark a system `[FixedTimestep]` to run it zero or more times per `Update` instead, at a constant interval:

```csharp
[FixedTimestep]
public sealed partial class PhysicsSystem : QuerySystem
{
    protected override IQuery DefineQuery(Query query) => query.With<Position, Velocity>();

    public void Update(Time time, ref Position position, in Velocity velocity) { /* ... */ }
}
```

Configure the interval on the builder, it defaults to 1/60s if you never call this:

```csharp
var world = new WorldBuilder()
    .WithFixedTimestep(TimeSpan.FromSeconds(1.0 / 60))
    .AddSystem<PhysicsSystem>()
    .Build();
```

:::note
A slow frame doesn't cause a spiral of catch-up steps. `maxSubstepsPerUpdate` (default 5) caps how many fixed steps one `Update` call can run, any backlog beyond that is dropped, not deferred to the next call.
:::

## Reading Time correctly

`Time.Delta`/`Time.Elapsed` is the *virtual* clock: scaled by `TimeScale`, frozen at zero delta while paused. `World.RealTime` is the wall-clock counterpart, unaffected by either. Use it for anything that needs to keep moving through a pause, a pause menu's own countdown, for instance.

## Pause and timescale

```csharp
world.Pause();
world.Resume();
world.TimeScale = 0.25; // slow motion
```

`Pause()` freezes virtual time, every system's `Time.Delta` becomes zero until `Resume()`. `TimeScale` keeps applying underneath a pause, so resuming continues at whatever scale was last set.

## Interpolating between fixed steps

A fixed-step system doesn't run every frame, so rendering its raw output looks stepped. `World.FixedStepAlpha` is how far the accumulator is into the next fixed step, blend your rendered position between the previous and current simulated state by this fraction:

```csharp
var rendered = Vector2.Lerp(previousPosition, currentPosition, (float)world.FixedStepAlpha);
```

## Next

Timestep and ordering both shape when code runs. For how entities relate to each other structurally, see [Relations](/build/ecs/relations/).
