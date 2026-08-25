---
title: Command Buffer
description: Why structural changes defer, and how to use CommandBuffer directly.
---

Two systems in the same parallel stage might both want to destroy an entity, or add a component to one the other is mid-iteration over. Mutating archetype storage directly while another system might be reading it has no guard against corruption, so structural changes never happen immediately. They queue instead, then apply in one deterministic pass.

That's the piece that makes [systems running in parallel with no thread code](/guides/systems/#the-parallel-scheduler) actually hold together.

## Applying the queue

```csharp
goblin.DestroyEntity();
world.ApplyCommands();
```

`goblin.DestroyEntity()` queues on `world.Commands`, the world's default `CommandBuffer`, same as calling `world.Commands.DestroyEntity(goblin)` directly. `ApplyCommands()` replays everything queued since the last call, then clears it. Nothing queued is visible to a query until this runs, see [Entities & components](/build/ecs/entities-and-components/#creating-entities) for the creation-side version of this.

Several systems can queue against the same buffer at once, from different threads in the same parallel stage, safely.

## A private buffer

`World.CreateCommands()` hands back a fresh `CommandBuffer` of your own, separate from `world.Commands`:

```csharp
var commands = world.CreateCommands();
commands.DestroyEntity(goblin);
// queue more, from wherever you like
world.ApplyCommands(commands);
```

Useful for building up a batch of changes somewhere other than inline in a system's `Update`, then applying them together on your own schedule.

## Next

A system that depends on another running first, not just on shared data, needs to say so explicitly. See [System Ordering](/guides/system-ordering/).
