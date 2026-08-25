---
title: Relations
description: Structural links between entities, beyond components and tags.
---

A component describes an entity. A relation describes an edge between two: `Targeting`, `MountedOn`, `Owns`, whatever your game needs. It's a third category alongside components and tags, not a variant of either.

## Declaring a relation

```csharp
struct Targeting : IRelation { public float ThreatLevel; }
```

`IRelation` is a marker interface. The struct's type is the relation's identity, distinguishing `Targeting` from `Owns` even if both carried the same fields. Any fields on it are the payload carried by each edge, an empty struct is a marker-only relation with nothing to say beyond "this edge exists."

## Adding and removing edges

```csharp
goblin.AddRelation<Targeting>(player, new Targeting { ThreatLevel = 0.8f });
goblin.RemoveRelation<Targeting>(player);
world.ApplyCommands();
```

`AddRelation`/`RemoveRelation` are `EntityView` mutators, same chaining as components. The direct `world.Commands.AddRelation<Targeting>(goblin, player, ...)` form works too, queuing the exact same thing.

## One target or many

By default a relation can have any number of targets: nothing stops `goblin` from having a `Targeting` edge to several entities at once, tracking multiple threats. A simple AI wants exactly one active target instead, implement `IExclusiveRelation` alongside `IRelation`:

```csharp
struct Targeting : IRelation, IExclusiveRelation { public float ThreatLevel; }
```

Targeting a new entity now replaces the old one rather than accumulating alongside it, `goblin`'s attention snaps to whichever threat it targeted most recently.

## Cascading destruction

Implement `IDependent` to make destroying the target recursively destroy every entity with an edge pointing at it, instead of just unlinking them:

```csharp
struct MountedOn : IRelation, IDependent { }

turret.AddRelation<MountedOn>(tank);
```

Destroy the `tank`, and every `turret` `MountedOn` it goes with it.

:::tip
Filter queries by relation with `WithRelation`/`WithoutRelation`, see [Queries](/build/ecs/queries/#filtering-by-relation).
:::

:::tip
For how an edge is actually stored, see [Relations](/understand/relations/).
:::

## Next

Wyrd ships one relation built in, for the most common case of all. See [Parent/Child](/build/ecs/relations/parent-child/).
