---
title: Debugging
description: Inspecting live world state, every archetype, every entity, every component value.
---

Sometimes you need to see what the world actually contains right now: which archetypes exist, how many entities are in each, what a specific entity's components hold. `World.EnumerateArchetypes`/`EnumerateEntities` resolve every component, tag, and relation type to its debug name for you.

## Reading world state

```csharp
foreach (var archetype in world.EnumerateArchetypes())
{
    Console.WriteLine($"{archetype.EntityCount} entities: {string.Join(", ", archetype.ComponentDiscriminators)} {string.Join(", ", archetype.TagDiscriminators)}");
}
```

One entry per archetype with at least one live entity: its count, and the debug name of every component and tag type on it.

```csharp
foreach (var entity in world.EnumerateEntities())
{
    foreach (var component in entity.Components)
        Console.WriteLine($"{entity.Entity}: {component.Discriminator}");
}
```

One entry per live entity, by name only, no byte payloads.

For a component's actual encoded bytes too, not just its name, pass a `CodecRegistry` with the types you care about registered, the same object [Persistence](/guides/persistence/) uses for real save/load:

```csharp
foreach (var entity in world.EnumerateEntities(registry))
{
    foreach (var component in entity.Components)
        Console.WriteLine($"{entity.Entity}: {component.Discriminator} = {component.Data.Length} bytes");
}
```

A component with no registered codec still appears, by name, with an empty `Data` array. Each component comes back as an `EncodedComponent`, the same encoded form persistence writes to disk, decode it with whatever codec registered it.

:::note
This is a debug/tooling path, not a per-tick one, both calls eagerly materialize a full snapshot. Gate call sites behind `#if DEBUG`/`[Conditional("DEBUG")]` to keep them out of a trimmed or Native AOT Release publish.
:::

:::note
This is the data layer, not a finished tool. There's no built-in viewer or CLI dumper on top yet, bring your own consumer for now.
:::

## Next

For persisting that same state to disk instead of just inspecting it, see [Persistence](/guides/persistence/).
