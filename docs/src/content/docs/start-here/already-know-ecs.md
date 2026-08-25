---
title: Already know ECS?
description: What's distinctive about Wyrd, for developers who already know entities, components, and systems.
---

This skips the introduction. If you've used an archetype or sparse-set ECS before, here's what to know before writing Wyrd code.

## Parallel by default

Register systems with `WorldBuilder`, call `world.Update(...)` once a tick, and the scheduler runs what it can in parallel without you writing any thread code. It looks at what each system reads and writes, groups the systems with no conflicts into a stage, and runs each stage inline or on the thread pool depending on world size. See [Systems](/build/game-loop/systems/) for the API, [Scheduling](/understand/scheduling/) and [Parallel Execution](/understand/parallel-execution/) for how the stage graph and the thread dispatch actually work.

## Source-generated, not reflected

Every `QuerySystem`, every fluent `.With<T1, T2>()` chain, and every persisted component gets its dispatch code, arity overloads, and serializers generated at compile time by Roslyn incremental generators, not resolved through reflection at runtime. See [Source Generation](/understand/source-generation/).

## Structural changes go through a command buffer

Spawning, despawning, and adding or removing a component never happen immediately, they queue on a `CommandBuffer` and apply in one deterministic pass. That's what makes systems safe to run in parallel and mutate freely from inside a `ParallelForEach`. See [Command Buffer](/build/game-loop/systems/command-buffer/) and [Structural Changes](/understand/structural-changes/).

## Relations are a first-class category

Components describe an entity, relations describe an edge between two: `Targeting`, `MountedOn`, parent/child. `IRelation` is its own category alongside `IComponent` and `ITag`, with exclusive (single-target) and cascading-destroy variants built in. See [Relations](/build/ecs/relations/) and [the internals](/understand/relations/).

## Persistence is a package away

Reference `Wyrd.Ecs.Persistence.Binary` or `.Json`, call one method on `WorldBuilder`, and every component is included by default. Layer continuous, crash-safe WAL persistence on top with one more method call. See [Persistence](/build/persistence/).

## Archetype storage, two entity identifiers

Components are stored as dense columnar arrays per archetype, one array per component type, and entities move between archetypes as their component set changes. Identity is split in two: `Entity` (an id plus a generation, reused after despawn, process-local) for everyday work, and `EntityId` (a permanent 128-bit value) for anything that needs to outlive the process, saves, cross-process references. See [ECS Architecture](/understand/ecs-architecture/).

## Native AOT throughout

The core library is `IsAotCompatible`, backed by dedicated AOT smoke-test projects that actually publish and run. The generated code is closed generic types and delegates, no reflection anywhere in it, so trimming and Native AOT hold up in practice.

## Next

[Wyrd in 10 minutes](/start-here/wyrd-in-10-minutes/) is still worth skimming for the exact API shapes above, even if the concepts are familiar.
