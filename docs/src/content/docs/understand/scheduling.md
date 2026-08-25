---
title: Scheduling
description: How systems are grouped into stages, phases, and fixed vs. variable cadence, under the hood of the parallel scheduler.
---

[Systems](/build/game-loop/systems/#the-parallel-scheduler) and [System Ordering](/build/game-loop/system-ordering/) cover the scheduler's surface: systems run in parallel automatically, and `[RunBefore]`/`[RunAfter]` add explicit ordering. This is how that's actually built.

## Reads and writes become a stage graph

Every system's `Update` gets its read/write set generated alongside its dispatch code, as a bitset over component types. The stage planner walks the registered systems in order and greedily packs each one into the first stage whose systems don't conflict with it, conflict meaning either side writes a type the other reads or writes. A system with no generated access information, one that doesn't declare its reads and writes through the usual `QuerySystem` shape, gets the conservative default instead: its own exclusive stage, so it never risks running alongside something it might conflict with.

## RunBefore and RunAfter add edges to the same graph

`[RunBefore(typeof(X))]`/`[RunAfter(typeof(X))]` and the runtime `.Before<T>()`/`.After<T>()` calls add edges to a graph whose nodes are system instances and, where used, marker types. That graph is resolved and stably topologically sorted before the conflict-based packing above runs, so ordering constraints are respected first and data conflicts decide packing within what ordering already allows. A genuine cycle, two systems each declared to run before the other, throws at build time, naming the cycle.

## Phase is ordering sugar over two markers

`[Phase(Phase.PreUpdate)]`/`[Phase(Phase.PostUpdate)]` (the default is `Phase.Update`) aren't a separate scheduling mechanism, they translate directly into `[RunBefore]`/`[RunAfter]` edges against two marker types, `StartOfUpdatePhase` and `EndOfUpdatePhase`, that are never registered or instantiated, only ever targeted. A world where nothing uses `Phase` never gains those edges at all.

## Fixed and variable cadence are two separate stage lists

`[FixedTimestep]` puts a system on the accumulator-driven fixed cadence instead of the default variable one, and the two cadences are scheduled as entirely separate stage lists: every fixed-cadence stage runs to completion before any variable-cadence stage starts, within one `world.Update(...)` call. An ordering edge between a fixed and a variable system is a build-time error, the two lists never interleave, so there'd be nothing for it to mean.

## Stage layout is deterministic, not stable across reordering

Ties in the topological sort break by registration order, the order `AddSystem<T>()` was called, so a fixed registration order always produces the same stages. Reordering `AddSystem` calls without adding an explicit edge between the affected systems can change which stage each one lands in, since the tie-break itself moved.

## Next

[Parallel Execution](/understand/parallel-execution/) covers what happens once a stage is decided, how it actually runs on the thread pool.
