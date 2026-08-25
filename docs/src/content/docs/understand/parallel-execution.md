---
title: Parallel Execution
description: How stage-level and query-level parallelism dispatch, chunk, and stay thread-safe.
---

[Systems](/build/game-loop/systems/#the-parallel-scheduler) covers systems running in parallel automatically, and [Queries](/build/ecs/queries/#parallelforeach) covers opting a query into `.ParallelForEach`. Underneath, they're two independent layers, both built on `System.Threading.Tasks.Parallel.ForEach`, nothing else.

## Stage-level parallelism is size-gated, query-level isn't

A stage of non-conflicting systems (see [Scheduling](/understand/scheduling/)) only actually dispatches to the thread pool when the world's total entity count clears a threshold, 1,000 by default, configurable via `WorldBuilder.WithParallelThreshold`. Below that, the stage runs inline, sequentially, since spinning up tasks for a handful of entities costs more than it saves. A `.ParallelForEach` call on a query has no such threshold, it dispatches to the thread pool every time it's called, the decision to parallelize that one query is yours, made by choosing `.ParallelForEach` over `.ForEach` at the call site.

## Work is sliced by row range, not by whole archetype

Resolving a query's matching archetypes normally yields one chunk per archetype, but an archetype larger than 4,096 rows splits into consecutive row-range slices instead, so one huge archetype still spreads across every available core rather than running as a single unparallelizable unit. Each slice's row indices are relative to the slice, not the archetype, index 0 inside a `ParallelForEach` callback always means the first row of that thread's own range. A hundred-thousand-row archetype across sixteen cores resolves in around eleven microseconds this way.

## Mutating your own row is safe without a lock, shared state isn't

Slices never overlap, and each thread only ever touches the rows in its own slice, so writing to a component through the `ref`/`in` parameters `ForEach`/`ParallelForEach` hand you is safe without a lock, that row belongs to that thread alone for the duration of the call. Anything read or written outside those parameters, a captured variable, a field on some other object, is shared across every thread running the callback and needs its own synchronization, `Interlocked` or a lock, same as any other concurrent code.

## Structural changes serialize on a lock, not on per-thread buffers

There's no per-thread command buffer that gets merged after a `ParallelForEach` finishes. `CommandBuffer`'s public methods each take the same internal lock, so every thread queuing a structural change during a parallel callback is safe, but briefly serialized, at the moment it enqueues, not for the rest of its work. `World.CreateCommands()` hands back an independent buffer for a caller that wants to avoid even that brief contention, or batch changes separately, applying it is a separate `World.ApplyCommands(buffer)` call, in whatever order the caller chooses. Two buffers combine by being applied one after the other, there's no automatic merge step beyond that.

## Next

[Structural Changes](/understand/structural-changes/) covers what actually happens when `ApplyCommands` runs.
