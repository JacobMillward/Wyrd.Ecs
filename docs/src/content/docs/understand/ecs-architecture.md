---
title: ECS Architecture
description: How Wyrd stores entities and components, and how entity identity works underneath Entity.
---

[Entities & Components](/build/ecs/entities-and-components/) and [Queries](/build/ecs/queries/) cover the API. This is what's underneath it: how entities are actually stored, and why an entity's shape changing means it physically moves.

## Archetypes are dense, columnar storage

Every entity with the exact same set of components and tags lives in the same archetype, a dense, parallel-array store: one array per component type, one entity list running alongside it at the same row indices. A `Position` and a `Velocity` on the same entity sit at the same row in two separate arrays, not in one struct together, that's what makes a query's `ForEach` a tight loop over contiguous memory instead of a scattered walk.

Tags cost nothing to store: `ITag` only ever contributes a bit to the archetype's signature, there's no array for it to occupy.

## Adding or removing a component moves the entity

An archetype's signature is fixed, so adding or removing a component can't just resize a row in place, it moves the entity to a different archetype: a new archetype if nothing else has that exact shape yet, or an existing one if something does. The move copies every shared component's value across, then closes the gap it left behind by swapping the archetype's last row into the vacated slot. That's also why this counts as a [structural change](/understand/structural-changes/) and goes through a command buffer rather than happening inline mid-query.

Archetypes cache their add/remove transitions per type: once one entity has taken the `Health`-adding edge from a given starting archetype, the next entity taking the same edge from the same starting shape reuses it.

## Two identifiers, two lifetimes

`Entity` is a working id: an integer plus a generation, handed out lazily and reused after an entity is destroyed. The generation is what makes reuse safe, a stale `Entity` from before a destroy carries the old generation, and a liveness check against the current one rejects it rather than silently addressing whatever got the recycled slot next. `Entity` never leaves the process, don't put one in a save file or send it over the network.

`EntityId` is the other half: a permanent, opaque 128-bit value, minted the first time something actually needs one, a save, a relation that has to survive a reload, a reference handed to another process. Relations and persistence are built on `EntityId` for exactly that reason, an edge or a save record has to outlive the `Entity` that existed when it was written.

## Next

[Queries](/understand/queries/) covers how a query resolves against these archetypes at compile time.
