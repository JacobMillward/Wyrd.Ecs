---
title: Resources
description: World-scoped singleton state for data that belongs to no single entity.
---

Not everything is a component. The player's score, an input snapshot, a reference to the audio backend - state that exists once per world, not once per entity. Making it a component would mean inventing a singleton entity to hold it. Resources are the direct version: a struct registered on the world, readable and writable from anywhere.

## Declaring and registering

```csharp
public struct Score : IResource
{
    public int Value;
}

var world = new WorldBuilder()
    .AddResource(new Score { Value = 0 })
    .AddSystem<ScoreSystem>()
    .Build();
```

`IResource` is a marker interface, same shape as `IComponent`. Registration also works after `Build()` with `world.AddResource(...)`, and a factory overload receives the world itself for resources that need to look at it first:

```csharp
world.AddResource(w => new CameraRig { Main = SpawnCamera(w) });
```

One resource per type - registering a second throws. Call `RemoveResource<T>()` first to replace one.

## Reading and writing

```csharp
world.GetResource<Score>();            // a copy
world.GetResourceRef<Score>().Value++; // a mutable reference into storage
world.TryGetResource<Score>(out var score); // no throw when absent
```

`GetResource` hands back a copy, mutating it changes nothing the world can see. `GetResourceRef` returns a `ref T` into the world's own storage, writes through it are visible to every later reader. `RemoveResource<T>()` takes one out again.

## Injecting into systems

A `[Resource]` property on a `QuerySystem` is refreshed from the world automatically:

```csharp
public sealed partial class ScoreReaderSystem : QuerySystem
{
    [Resource] public Score Score { get; private set; }

    protected override IQuery DefineQuery(Query query) => query;

    public void Update(Time time)
    {
        Console.WriteLine($"Score is {Score.Value}");
    }
}
```

A system that *owns* the score declares a public setter instead and assigns a new value before returning:

```csharp
public sealed partial class ScoreWriterSystem : QuerySystem
{
    [Resource] public Score Score { get; set; }

    protected override IQuery DefineQuery(Query query) => query.With<KillConfirmed>();

    public void Update(Time time, ref KillConfirmed kill)
    {
        Score = new Score { Value = Score.Value + kill.Points };
    }
}
```

The setter's accessibility is the contract. A get-only or private-set property is read-only: fetched fresh before every `Update`, and the scheduler only counts the system as reading the resource. A public setter makes it read-write: whatever `Update` leaves in the property is written back to the world afterwards, and the scheduler counts the system as writing it, two systems writing the same resource won't share a parallel stage.

:::caution
The property's value is only valid inside `Update`. Copying it into a field captures a stale snapshot - the generator refreshes the property every tick but not your field. Storing a resource value somewhere longer-lived produces a build warning (WYRD008).
:::

:::note
Declaring a public setter and then never assigning to it also warns (WYRD009). Write access costs scheduling freedom, so an unused one is treated as probably unintended - drop the public setter unless writes are real.
:::

`[Resource]` is a `QuerySystem` feature. A plain `EcsSystem` reads resources through `world.GetResource<T>()`/`GetResourceRef<T>()` inside `Execute`.

## Next

Resources are shared state systems read. For a system telling another system that something happened, rather than both reading the same state, see [Events](/guides/events/).
