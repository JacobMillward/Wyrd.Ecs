---
title: Queries
description: The fluent query chain, filtering, and running work across matching entities.
---

A query describes a shape of entity: which components it has, and which it doesn't. `world.Query()` starts the chain.

## Matching components with With

```csharp
world.Query().With<Position, Velocity>()
```

matches every entity that has both a `Position` and a `Velocity`. `.With<A, B, C>()` collapses what would otherwise be three chained `.With<A>().With<B>().With<C>()` calls into one.

:::note
`With`, [`Without`](#without), and [`Has`](#has) take up to 8 type arguments in one call. [`Any`](#any) takes up to 8 as well, starting from 2.
:::

:::caution
`With` only accepts `IComponent` types, it binds each one to a `ForEach` parameter, and a tag has no data to bind. `With<SomeTag>()` won't compile. Filter on tags with [`Without`](#without), [`Has`](#has), or [`Any`](#any) instead, see the table below.
:::

## Running work

### ForEach

```csharp
world.Query().With<Position, Velocity>()
    .ForEach((ref position, in velocity) =>
    {
        position.X += velocity.X;
        position.Y += velocity.Y;
    });
```

Whether a component is read-only or mutable comes from `ref`/`in` on the callback's parameters themselves, in the same order as the `With` calls that requested them.

### ParallelForEach

For CPU-heavy per-entity work, `.ParallelForEach` runs the same shape across the thread pool instead of inline.

```csharp
var total = 0;
world.Query().With<Position>()
    .ParallelForEach(0, (in int _, ref Position position) => Interlocked.Increment(ref total));
```

:::caution
Your callback runs concurrently across threads. Mutating shared state, like `total` above, needs `Interlocked` or a lock. Each entity's own components are always safe to mutate directly, they belong to that thread's row alone.
:::

:::tip
For how the underlying archetypes get sliced across threads, see [Parallel Execution](/understand/parallel-execution/).
:::

## Conditional filters

`Without`, `Has`, and `Any` only narrow the query's runtime filter, they never change its shape the way [`With`](#matching-components-with-with) does. That means each of them can be applied conditionally, and the result still compiles and matches the way you'd expect.

Unlike `With`, none of the three bind data to a `ForEach` parameter, so all three accept `IComponent` and `ITag` equally:

| Filter | `IComponent` | `ITag` |
| --- | --- | --- |
| [`With`](#matching-components-with-with) | yes, binds its data | no, nothing to bind |
| [`Without`](#without) | yes | yes |
| [`Has`](#has) | yes | yes |
| [`Any`](#any) | yes | yes |

### Without

Excludes entities with the given components or tags:

```csharp
struct Frozen : ITag { }

world.Query().With<Position, Velocity>().Without<Frozen>()
```

matches every entity with a `Position` and a `Velocity`, and no `Frozen`.

### Has

Requires a component or tag without binding it to a [`ForEach`](#foreach) parameter, unlike `With`. Reach for `Has` when you only need to know something's there, not read its data:

```csharp
world.Query().With<Position>().Has<Frozen>()
    .ForEach((ref Position position) =>
    {
        // every match has Frozen, there's nothing to read from a tag anyway
    });
```

`Frozen` is a tag, so [`With<Frozen>()`](#matching-components-with-with) wouldn't even compile here, `With` only binds `IComponent` types, and a tag has no data to bind. `Has` works on either, that's exactly why it exists: `Has<Position>()` is legal too, for when you need presence without the data.

### Any

Matches if at least one of the given components or tags is present, common with tags for "any status effect" style checks:

```csharp
struct Burning : ITag { }
struct Chilled : ITag { }

world.Query().With<Position>().Any<Burning, Chilled>()
```

matches every entity with a `Position` that has `Burning`, `Chilled`, or both.

### Applying them conditionally

Because none of the three change the query's shape, they can be assigned back conditionally:

```csharp
struct HardcoreOnly : ITag { }

var query = world.Query().With<Position>();
if (hardcoreMode) query = query.Has<HardcoreOnly>();
```

## Filtering by relation

`WithRelation`/`WithoutRelation` filter on relation edges, the structural links `AddRelation` creates between entities. `WithRelation<T>()` matches any entity with at least one edge of that relation, target unspecified. `WithoutRelation<T>()` excludes them. A relation is neither an `IComponent` nor an `ITag`, it's its own `IRelation` type, a third category the table above doesn't cover.

```csharp
struct Targeting : IRelation { public float ThreatLevel; }

world.Query().WithRelation<Targeting>()
    .ForEach(0, (in int _, in RelationLinks<Targeting> link) => { /* one match per entity with a Targeting edge */ });

world.Query().With<Position>().WithoutRelation<Targeting>()
```

:::note[Advanced]
`WithRelation<T>()` is shorthand for `.With<RelationLinks<T>>()`, and `WithoutRelation<T>()` for `.Without<RelationLinks<T>>()`. They read as intent at the call site, that's the only difference.
:::

:::tip
For how a query chain compiles down to a cached `ArchetypeQuery`, see [Queries](/understand/queries/).
:::

## Next

A query you build once and run every tick is a system. See [Systems](/build/game-loop/systems/).
