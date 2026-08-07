---
title: Entities & components
description: Components, tags, and how entities get created, changed, and destroyed.
---

An entity is just an ID. What makes it interesting is the components attached to it.

## Components and tags

A component is a struct implementing `IComponent`. It carries data, and it's the unit archetype storage groups by.

```csharp
public struct Health : IComponent { public int Current; public int Max; }
```

A tag is a struct implementing `ITag` instead. It carries no data, it just marks an entity as having some property.

```csharp
public struct Stunned : ITag { }
```

## Creating entities

All structural changes, creating entities, adding or removing components, go through a `CommandBuffer`. `world.Commands` is the world's default one.

```csharp
Entity goblin = world.Commands.CreateEntity()
    .AddComponent(new Health { Current = 10, Max = 10 })
    .AddTag<Stunned>();
world.ApplyCommands();
```

Nothing is queued into the world until `world.ApplyCommands()` runs. A [query](/guides/queries/) never sees a half-finished change mid-iteration.

## The EntityView shortcut

`CreateEntity()` returns an `EntityView`, a chainable handle bound to one entity and the buffer it queues on. Once you have one, every mutation is a direct call on it:

```csharp
goblin.RemoveTag<Stunned>()
    .AddComponent(new Health { Current = 5, Max = 10 });
world.ApplyCommands();
```

:::tip
`EntityView` is a `ref struct`, it can't be stored in a field, a list, or captured by a lambda, only ever a local variable or a chained expression. If all you have is a plain `Entity` you stored earlier, `world[goblin]` gets you an `EntityView` back for it.
:::

See [Systems](/guides/systems/#optional-update-parameters) for how a system's `Update` gets one automatically.

:::note[Advanced]
`goblin.AddComponent(...)` and `world.Commands.AddComponent(goblin, ...)` queue the exact same thing, `EntityView` is a thin wrapper. The direct form skips constructing a view, worth it only in a hot per-entity loop.
:::

## Destroying entities

```csharp
goblin.DestroyEntity();
world.ApplyCommands();
```

## Next

Once you can create entities, the next step is asking which ones have what. See [Queries](/guides/queries/).
