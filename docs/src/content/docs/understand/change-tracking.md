---
title: Change Tracking
description: Finding out what changed in a world - component values, structural changes, entity lifecycles - after the fact.
---

Events cover signals a system emits deliberately. Change tracking answers the other question: what actually happened to the world? A `Health` that dropped below zero, a `Frozen` tag appearing on the player, an entity destroyed by something you've never heard of. There are two tiers, a typed buffered one and a raw synchronous one.

## Subscribing to a component's changes

```csharp
public sealed partial class LowHealthSystem(World world) : QuerySystem
{
    private readonly ChangeSubscription _healthChanges = world.Subscribe<Health>();

    protected override IQuery DefineQuery(Query query) => query;

    public void Update(Time time)
    {
        foreach (var change in _healthChanges.Drain())
        {
            if (change.Kind is ChangeKind.ValueChanged)
            {
                var health = (Health)change.Value!;
                // health.Current is what the value became this tick
            }
        }
    }
}
```

`Subscribe<T>()` reports three kinds of change for one component type: its value changing (`ValueChanged`, with the new value), and it being added to or removed from an existing entity (`ComponentAdded`/`ComponentRemoved`). `Drain()` returns everything recorded since your last call and clears the buffer - call it once per tick like an event reader, or less often, from any thread, whatever suits the consumer.

Subscriptions are independent, each drains its own private buffer. Behind the scenes the per-type scan runs at most once per tick no matter how many subscribers watch the same type, so five systems watching `Health` cost one scan.

:::note
A value change is recorded when the write goes through any of the public component APIs - `GetComponent<T>`, a query's `ref` parameter, `Mut<T>` spans. Tracking for a type turns on with the first subscription and off when the last one disposes; writes made while nothing subscribes leave no trace.
:::

## Tags, relations, and entity lifecycle

The same subscription pattern covers everything else that can change, each scoped to what makes sense for its category:

```csharp
world.SubscribeTag<Frozen>();        // TagAdded / TagRemoved
world.SubscribeRelation<Targeting>(); // RelationLinked / RelationUnlinked, Related = edge target
world.SubscribeEntityLifecycle();     // EntityCreated / EntityDestroyed, world-scoped
```

For consumers that don't know their component type at compile time - a persistence layer walking a `CodecRegistry`, say - `world.Subscribe(codec)` watches the codec's type instead.

## Reading an entry

Every kind arrives as the same `ChangeEntry`:

| Field | Meaning |
|---|---|
| `Entity` | The entity that changed |
| `Kind` | Which of the nine `ChangeKind` values this is |
| `Value` | For `ValueChanged`: the component's current value, boxed. `null` otherwise |
| `Related` | For relation kinds: the edge's other end. `Entity.Null` otherwise |
| `TypeIndex` | The component/tag/relation's runtime index. `null` for lifecycle entries |
| `Tick` | When it happened |

## Under the hood

There's no separate change log to maintain. Each component's storage keeps one extra array alongside the component's own, a per-row tick marking when that row was last written. A tick advancing triggers the scan, once per tracked type regardless of how many subscribers are watching it, each scanner keeping its own watermark so a subscriber that joins mid-scan catches up on the next tick instead of sharing another one's progress. `Drain()` itself runs no scan at all, it just swaps out the buffer of entries that tick's scan already published.

## Watching structural changes synchronously

When a buffered report is too late - maintaining an external index, feeding a profiler, mirroring state out of the world entirely - `ObserveStructuralChanges` fires inline at the exact moment of mutation, including deferred ones the moment a command buffer applies them:

```csharp
var handle = world.ObserveStructuralChanges(new MyObserver());
// ...
handle.Dispose();
```

An observer implements `IStructuralChangeObserver`: entity created/destroyed, component/tag added/removed, relation linked/unlinked. The two relation callbacks are optional, defaulting to no-ops.

This tier trades convenience for immediacy. Callbacks arrive as bare `(Entity, int typeIndex)` pairs - the runtime-only index, with no public way to map it back to a type - so it fits bookkeeping that doesn't care *which* component was involved. When the type matters, subscribe instead.

:::caution
Registering and disposing observers isn't thread-safe against each other. Subscribe and unsubscribe from one place, typically before building the world or during startup.
:::

## Events or change tracking?

- **[Events](/build/ecs/events/)**: a system deliberately announces something, with its own payload. You designed the signal.
- **A subscription**: you want to react to state changes you didn't instrument, including what a value became. The world tells you.
- **A structural observer**: you need to know the instant anything structurally moves, without buffering, and don't need types.

## Next

For inspecting live world state rather than reacting to its changes, see [Debugging](/build/debugging/).
