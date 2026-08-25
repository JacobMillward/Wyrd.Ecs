---
title: Structural Changes
description: Why spawning, despawning, and add/remove-component defer through a command buffer instead of applying immediately.
---

[Command Buffer](/build/game-loop/systems/command-buffer/) covers using `CommandBuffer` directly. This is why it exists at all.

## What counts as structural

Creating or destroying an entity, and adding or removing a component or tag, count as structural changes. Changing archetype is the mechanism behind add/remove, not a separate case, an entity's shape changing is always a move to a different archetype. Reading or writing a value already on an entity never touches archetype row layout, so that stays direct, it's the only kind of mutation that doesn't go through a command buffer.

## Why it can't happen mid-iteration

An archetype stores its entities as dense parallel arrays. Removing a row is a swap-remove, the archetype's last row gets copied into the removed row's slot so the arrays stay dense with no holes. A query walking a row range grabs the live backing array once for that range, so a swap-remove happening mid-walk could substitute a different entity's data into a row the walk hasn't reached yet, or has already passed. Growth compounds it: an archetype whose arrays are full reallocates new, larger ones, and any reference taken before that reallocation now points at a disconnected array. Adding or removing a component moves the entity to a different archetype entirely, so mid-walk it could vanish from the array being walked, or appear in one the walk has already passed or hasn't reached, an ordering hazard on top of the memory-safety one.

## The command buffer records now, applies later

Recording a command enqueues it into a buffer-local queue guarded by a single lock, safe to call from multiple threads at once. Commands that carry a value, `AddComponent<T>`, stash that value in a per-type buffer alongside the queue. `World.ApplyCommands()` replays everything queued since the last call against live storage, in order, then clears it, an earlier destroy silently no-ops a later command targeting the same now-dead entity rather than throwing.

## Applying resets only what a batch touched

Applying a batch resets only the per-type payload buffers that batch's commands actually touched, tracked incrementally as each command is queued. The cost of that reset scales with how many distinct component types one batch used, not with how many types have ever passed through the buffer over its lifetime.

## No automatic merge across buffers

`World.CreateCommands()` hands back an independent buffer, nothing merges two buffers for you. Applying both is two separate `ApplyCommands(buffer)` calls, in whichever order the caller picks, since which change should win when two buffers touch the same entity is a decision the caller is in a better position to make than the buffer itself.

## Next

[Relations](/understand/relations/) covers how a relation edge is stored, since adding one is a structural change like any other.
