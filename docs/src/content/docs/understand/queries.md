---
title: Queries
description: How a query chain compiles away, and why With binds data while the others only filter.
---

[Queries](/build/ecs/queries/) covers the fluent API. Here's what a `.With<Position, Velocity>().ForEach(...)` chain actually becomes.

## A shape is resolved once, not per call

A source generator recognizes a query chain at compile time, by its exact sequence of `With`/`Without`/`Has`/`Any` calls and their type arguments, and emits a small static class per distinct shape that builds and caches one `ArchetypeQuery` for it. Write the same shape in two different systems and both call sites share the generated class and its cached query, there's exactly one `ArchetypeQuery` built for that shape, not one per call site or one per tick.

## ArchetypeQuery is a value, not an allocation

`ArchetypeQuery` is a `readonly struct`, so the query cached per shape above is a value, not a heap reference. Copying it to pass along costs only its fields, no allocation, no extra indirection to chase when a `ForEach` reads it every tick.

## With generates per shape, the filters generate once

`With`'s generated `ForEach`/`ParallelForEach` overloads exist for the exact tuple type your code actually asked for, `ref`/`in` on each parameter matching whether the generator saw that type written or only read. `Without`, `Has`, and `Any` work differently: their arity overloads (two types, three types, up to the arity cap) are templated once, for every arity, by a second generator that runs unconditionally at build time, not triggered by any particular call site. That's the mechanical reason `With` binds data to `ForEach` and the other three don't: `With`'s overloads are generated to match a callback signature, the filters' overloads exist independent of any callback at all.

## Next

[Scheduling](/understand/scheduling/) covers how the systems built on these queries get grouped into stages.
