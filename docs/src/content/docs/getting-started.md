---
title: Getting started
description: Install Wyrd.Ecs, define a component, and write your first system.
---

Wyrd.Ecs is an archetype ECS for .NET 10. This walks through the smallest useful setup: a component, a system, and the entities it acts on.

:::note
Wyrd.Ecs hasn't shipped to NuGet yet. Reference the project directly (`dotnet add reference path/to/Wyrd.Ecs.csproj`), or build packages locally with `dotnet pack` and reference those.
:::

## Define a component

A [component](/guides/entities-and-components/#components-and-tags) is a struct implementing `IComponent`, carrying whatever data it needs.

```csharp
using Wyrd.Ecs;

public struct Position : IComponent { public float X; public float Y; }
public struct Velocity : IComponent { public float X; public float Y; }
```

## Write a system

A [`QuerySystem`](/guides/systems/) is how you act on entities: it declares what it wants with `DefineQuery`, and acts on it with `Update`. The class must be `partial`, a source generator fills in the dispatch.

```csharp
public sealed partial class MovementSystem : QuerySystem
{
    protected override IQuery DefineQuery(Query query) => query.With<Position, Velocity>();

    public void Update(Time time, ref Position position, in Velocity velocity)
    {
        position.X += velocity.X * (float)time.Delta.TotalSeconds;
        position.Y += velocity.Y * (float)time.Delta.TotalSeconds;
    }
}
```

Read and write access comes straight from `ref`/`in` on `Update`'s parameters.

## Build a world and add entities

Register the system with `WorldBuilder`, then create the entities it'll act on.

```csharp
var world = new WorldBuilder()
    .AddSystem<MovementSystem>()
    .Build();

world.Commands.CreateEntity()
    .AddComponent(new Position())
    .AddComponent(new Velocity { X = 1, Y = 0 });
world.ApplyCommands();
```

:::note
Systems apply their own queued commands automatically between update stages. You only call `world.ApplyCommands()` yourself when mutating outside a system, like here.
:::

## Run it

Call `world.Update(...)` once per tick, and every registered system runs, in parallel where the scheduler can.

```csharp
world.Update(TimeSpan.FromSeconds(1.0 / 60));
```

### Running a single system without registering it

```csharp
world.RunOnce(new MovementSystem(), TimeSpan.FromSeconds(1.0 / 60));
```

runs a system once against a `World` directly, no `WorldBuilder` needed. Useful for a one-off system you don't want to register permanently.

## Next steps

- [Entities & components](/guides/entities-and-components/) covers tags, destroying entities, and adding components after creation.
- [Queries](/guides/queries/) covers filtering beyond `With`: `Without`, `Has`, `Any`, and running work across the entities that match.
- [Systems](/guides/systems/) covers optional `Update` parameters and how the scheduler parallelizes systems.
