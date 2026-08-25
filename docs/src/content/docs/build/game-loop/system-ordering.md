---
title: System Ordering
description: Making one system run before or after another, when a data conflict alone wouldn't force it.
---

The scheduler already separates two systems that read and write the same component. Sometimes you need more than that: a `SpawnSystem` might need to run before anything that reacts to newly spawned entities, even if the two never touch the same data. That's what explicit ordering is for.

## RunBefore and RunAfter

```csharp
[RunAfter(typeof(SpawnSystem))]
public sealed partial class ReactToSpawnsSystem : QuerySystem
{
    protected override IQuery DefineQuery(Query query) => query.With<Spawned>();

    public void Update(Time time, in Spawned spawned) { /* ... */ }
}
```

`[RunBefore(typeof(X))]`/`[RunAfter(typeof(X))]` declare that this system must run in a strictly earlier or later stage than `X`. Stack as many as you need, one attribute per target.

:::note
An edge like this can force two systems into separate stages even when neither reads nor writes anything the other touches. That's a real scheduling cost, reach for ordering only when the sequence actually matters.
:::

## Ordering against a whole phase

Naming every system in a phase to order against it doesn't scale. Define a marker instead, and order against that:

```csharp
public sealed class InputProcessed : MarkerSystem { }

[RunBefore(typeof(InputProcessed))]
public sealed partial class PlayerMoveInputSystem : QuerySystem { /* ... */ }

[RunBefore(typeof(InputProcessed))]
public sealed partial class PlayerActionInputSystem : QuerySystem { /* ... */ }

[RunAfter(typeof(InputProcessed))]
public sealed partial class AIDecisionSystem : QuerySystem { /* ... */ }
```

`AIDecisionSystem` now runs after every input system, whatever they turn out to be. Add a third input system later and it's covered automatically, nothing about `AIDecisionSystem` changes.

:::note
`MarkerSystem` is never registered or instantiated. It exists purely as a `Type` the ordering graph can target.
:::

## Ordering a system added at runtime

`AddSystem<T>()` returns a chainable registration, for systems added outside the attribute-based path:

```csharp
world.AddSystem<LateJoinerSystem>().After<InputProcessed>();
```

## Next

Ordering controls sequence. For controlling how often a system runs at all, see [Timestep, Pause & Timescale](/build/game-loop/timestep-pause-timescale/).
