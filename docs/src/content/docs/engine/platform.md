---
title: Platform
description: Open a window, read SDL's event pump, and react to devices connecting or disconnecting.
---

Every Engine package needs a window before it needs anything else. `Wyrd.Ecs.Platform` opens one and owns SDL's video subsystem for as long as `PlatformSystem` lives.

## Opening a window

```csharp
using Wyrd.Ecs.Platform;

var world = new WorldBuilder()
    .AddPlatform("My Game", 1280, 720)
    .Build();
```

`AddPlatform` registers a `PlatformSystem` that calls `SDL_Init(Video)` and creates the window immediately. Every other Engine package (`AddRenderer`, `AddInput`) resolves this system by type at `Build()` time, so `AddPlatform` has to come first in the chain.

:::note
An optional fourth argument takes `SDL.WindowFlags`, for things like `Hidden` or `Vulkan` in a headless test run.
:::

## Reading the window and events

```csharp
var platform = world.GetSystem<PlatformSystem>();
platform.Window;  // the native SDL window handle
platform.Events;  // every SDL event pumped this tick
```

`Window` is a raw `IntPtr`, for consumers reaching past this package into SDL3-CS directly. `Events` refills once per tick, before anything else runs, `PlatformSystem` schedules itself first.

## Devices connecting and disconnecting

```csharp
public sealed partial class DeviceLogSystem(World world) : QuerySystem
{
    private readonly EventReader<DeviceChange> _deviceChanges = world.CreateEventReader<DeviceChange>();

    protected override IQuery DefineQuery(Query query) => query;

    public void Update(Time time)
    {
        foreach (var change in _deviceChanges.Read())
        {
            // change.DeviceId, change.DeviceKind (Keyboard or Mouse), change.Change
        }
    }
}
```

`PlatformSystem` is the single source of `DeviceChange`, one event per real hot-plug, covering both directions so a reader can't handle connects while forgetting disconnects.

:::note
`DeviceKind` covers `Keyboard` and `Mouse` today. `Wyrd.Ecs.Input` already reacts to `DeviceChange` for you, see [Input](/engine/input/) and, for assigning specific devices to profiles, [Multi-Device](/advanced/input/multi-device/).
:::

## Next

[Renderer](/engine/renderer/) and [Input](/engine/input/) both build on the window `AddPlatform` opens.
