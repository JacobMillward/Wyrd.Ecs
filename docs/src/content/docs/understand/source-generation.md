---
title: Source Generation
description: What Wyrd generates at compile time, why, and what it buys over reflection.
---

[Already know ECS?](/start-here/already-know-ecs/) mentions this in passing: Wyrd's dispatch, query overloads, and serializers are generated at compile time by Roslyn incremental generators, not resolved through reflection at runtime. Here's what each one actually does.

## What gets generated

- **Query dispatch**: every distinct `.With<...>`/`.Without<...>`/`.Has<...>`/`.Any<...>` chain gets its own cached `ArchetypeQuery` and `ForEach`/`ParallelForEach` overloads on the exact tuple type it was written against, see [Queries](/understand/queries/). The same generator fills in a `QuerySystem`'s dispatch between `DefineQuery` and `Update`, and builds the reads/writes/ordering metadata the scheduler reads, see [Scheduling](/understand/scheduling/).
- **Query arity**: the `With`/`Without`/`Has`/`Any` overloads for two, three, and more type arguments, plus the `ArchetypeQuery`/`ArchetypeFilter` types those chains build against, are templated once for every supported arity, independent of anything your code actually calls.
- **Debug names**: implementing `IComponent`, `ITag`, or `IRelation` on a struct is itself what registers a human-readable debug name for it, disambiguated by its containing type when two types share a name.
- **Persistence**: the binary codec generates a `MemoryPackFormatter` per component with managed fields, hand-rolling the read/write calls rather than going through MemoryPack's generic path. The JSON codec and the shared tag/discriminator registration generate the equivalent registration calls, honoring `[StableName]`/`[RenamedFrom]` where you've set them.

## Why generation instead of reflection

The core library is `IsAotCompatible`, and three separate smoke-test projects publish with Native AOT and actually run, not just build. That only holds because none of the generated code touches reflection: everything is closed generic types and delegates resolved at compile time, a cached `ArchetypeQuery` or a generated formatter, not a runtime dictionary keyed by `Type`. The cost that would otherwise show up as a lookup or a boxing allocation per entity happens once, at compile time, instead.

## Generation triggers on declaration

The generators ship packaged as analyzers inside `Wyrd.Ecs` itself, and the persistence packages do the same for their own generators. Declaring an `IComponent`/`ITag`/`IRelation` struct, or writing a `.ForEach`/`QuerySystem`, is what triggers generation directly, the same act that makes a type usable is the act that generates its dispatch and serialization code.
