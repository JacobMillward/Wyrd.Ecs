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

`CreateEntity()` returns an `EntityView`, not the command buffer itself. It already knows which world and which entity, so each chained call reads `.AddComponent(...)` instead of `world.Commands.AddComponent(goblin, ...)`, no need to repeat which entity you mean.

:::tip
Assign it straight to an `Entity`, like `goblin` above, if you need to keep it around. Past that line, an `EntityView` can't be stored in a field, a list, or captured by a lambda, it only exists to make the moment of creation chainable.
:::

It shows up again as an optional parameter on a system's `Update`, for the same reason: mutating the entity currently being processed without re-specifying which one. See [Systems](/guides/systems/#optional-update-parameters).

## Changing an existing entity

For an entity you already have an `Entity` handle to (not the `EntityView` shortcut above), mutate it through the command buffer directly, passing the entity to every call:

```csharp
world.Commands.RemoveTag<Stunned>(goblin);
world.Commands.AddComponent(goblin, new Health { Current = 5, Max = 10 });
world.ApplyCommands();
```

## Destroying entities

```csharp
world.Commands.DestroyEntity(goblin);
world.ApplyCommands();
```

## Next

Once you can create entities, the next step is asking which ones have what. See [Queries](/guides/queries/).
