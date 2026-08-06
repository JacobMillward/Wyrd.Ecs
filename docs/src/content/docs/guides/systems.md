---
title: Systems
description: QuerySystem, registering systems, and how the parallel scheduler works.
---

A `QuerySystem` pairs a [query](/guides/queries/) with the code that runs against every entity it matches. `DefineQuery` declares the shape, `Update` is the per-entity body, a source generator fills in the dispatch between them.

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

## Optional Update parameters

`MovementSystem.Update` above takes `Time time` plus its components, that's the minimum. `World world` and/or [`EntityView entity`](/guides/entities-and-components/#the-entityview-shortcut) can slot in right after `time`, only when the system actually needs them:

```csharp
public void Update(Time time, ref Position position) { }
public void Update(Time time, World world, ref Position position) { }
public void Update(Time time, EntityView entity, ref Position position) { }
public void Update(Time time, World world, EntityView entity, ref Position position) { }
```

`EntityView` is the common one, it lets `Update` mutate the entity currently being processed, not just its components:

```csharp
public struct Dead : ITag { }

public sealed partial class DeathCheckSystem : QuerySystem
{
    protected override IQuery DefineQuery(Query query) => query.With<Health>();

    public void Update(Time time, EntityView entity, ref Health health)
    {
        if (health.Current <= 0) entity.AddTag<Dead>();
    }
}
```

## Registering systems

A single system you want to run once, without registering it, uses `world.RunOnce(...)` directly (see [Getting started](/getting-started/#running-a-single-system-without-registering-it)). For a real game loop, register systems with `WorldBuilder` and run every tick with `world.Update(...)`:

```csharp
var world = new WorldBuilder()
    .AddSystem<MovementSystem>()
    .AddSystem<DeathCheckSystem>()
    .Build();

world.Update(TimeSpan.FromSeconds(1.0 / 60));
```

## Constructing systems

`AddSystem<T>()` builds `T` for you, as long as it has a public parameterless constructor (or no constructor at all, which is the same thing) or a public constructor taking exactly one `World`:

```csharp
public sealed partial class SpawnerSystem(World world) : QuerySystem
{
    protected override IQuery DefineQuery(Query query) => query.With<Position>();

    public void Update(Time time, ref Position position) { /* ... */ }
}
```

There's no separate `OnCreate` hook, the constructor is it, and it only ever runs once. `ctor(World)` exists for exactly that case: one-time setup that needs the world being built, [`OnDestroy`](#enabling-disabling-and-changing-systems-at-runtime) is its teardown counterpart.

Anything else, extra parameters, more than one public constructor, is a compile error at the `AddSystem<T>()` call site. Construct it yourself with the `Func<World, T>` overload instead:

```csharp
public sealed partial class DamageOverTimeSystem(float multiplier) : QuerySystem
{
    protected override IQuery DefineQuery(Query query) => query.With<Health>();

    public void Update(Time time, ref Health health) => health.Current -= (int)(multiplier * time.Delta.TotalSeconds);
}

var world = new WorldBuilder()
    .AddSystem<DamageOverTimeSystem>(_ => new DamageOverTimeSystem(1.5f))
    .Build();
```

## The parallel scheduler

Systems run in parallel automatically. The scheduler looks at what each system reads and writes, groups the systems with no conflicts, and runs each group inline or on the thread pool depending on world size. No thread code needed on your side.

:::note[Advanced]
Two systems that touch the same component, one writing while the other reads or writes, can't share a group. The scheduler puts them in separate stages instead, so the conflicting reads and writes never run at the same time.
:::

## Enabling, disabling, and changing systems at runtime

```csharp
movementSystem.Enabled = false;
```

Systems can also be added or removed after the world is built, the same calls work at `Build()` time and at runtime:

```csharp
world.AddSystem<DeathCheckSystem>();
world.RemoveSystem<DeathCheckSystem>();
```

Removing a system calls its `OnDestroy`, once, the constructor's teardown counterpart. Since `DamageOverTimeSystem` is `partial`, override it in another declaration of the same class:

```csharp
public sealed partial class DamageOverTimeSystem
{
    protected override void OnDestroy() { /* release whatever the constructor set up */ }
}
```
