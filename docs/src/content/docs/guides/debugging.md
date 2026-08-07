---
title: Debugging
description: Inspecting live world state, every archetype, every entity, every component value.
---

Sometimes you need to see what the world actually contains right now: which archetypes exist, how many entities are in each, what a specific entity's components hold. `World.EnumerateArchetypes`/`EnumerateEntities` expose exactly that, given a `ComponentCodecRegistry` to resolve names.

## Getting a registry

If a persistence package is already referenced, one is already built for you, `world.DefaultComponentCodecRegistry`. Otherwise, build a small one for debugging alone:

```csharp
var registry = new ComponentCodecRegistry();
registry.RegisterTag<Enemy>("Enemy");
```

`RegisterTag` names tags, enough to see them in the output below. A component needs a real codec (`Register<T>`, the same work a persistence package generates for you) to show up too, one that isn't registered is silently skipped rather than erroring. See [Persistence](/guides/persistence/) for what a codec actually looks like.

## Every archetype

```csharp
foreach (var archetype in world.EnumerateArchetypes(registry))
{
    Console.WriteLine($"{archetype.EntityCount} entities: {string.Join(", ", archetype.ComponentDiscriminators)} {string.Join(", ", archetype.TagDiscriminators)}");
}
```

One entry per archetype with at least one live entity: its count, and the discriminators of every registered component and tag on it.

## Every entity

```csharp
foreach (var entity in world.EnumerateEntities(registry))
{
    foreach (var component in entity.Components)
        Console.WriteLine($"{entity.Entity}: {component.Discriminator} = {component.Data.Length} bytes");
}
```

One entry per live entity, including ones with no registered components or tags at all. Each component comes back as an `EncodedComponent`, the same encoded form persistence writes to disk, decode it with whatever codec registered it.

:::note
This is a debug/tooling path, not a per-tick one, both calls eagerly materialize a full snapshot. Gate call sites behind `#if DEBUG`/`[Conditional("DEBUG")]` to keep them out of a trimmed or Native AOT Release publish.
:::

:::note
This is the data layer, not a finished tool. There's no built-in viewer or CLI dumper on top yet, bring your own consumer for now.
:::

## Next

For persisting that same state to disk instead of just inspecting it, see [Persistence](/guides/persistence/).
