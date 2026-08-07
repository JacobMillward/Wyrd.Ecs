---
title: Persistence
description: Saving and loading world state, from a single save file to crash-safe incremental checkpoints.
---

If you want to persist world state to disk, Wyrd treats it as a package away, not a system you build yourself. Pick a codec, binary or JSON, then optionally layer continuous persistence on top.

## Pick a codec

### Binary (MemoryPack)

Reference `Wyrd.Ecs.Persistence.Binary` and mark each component you want saved:

```csharp
[MemoryPackable]
public partial struct Position : IComponent
{
    public float X;
    public float Y;
}
```

The builder call is a one-liner, already wired to every `[MemoryPackable]` component in your project:

```csharp
var world = new WorldBuilder()
    .AddBinaryPersistence("save.bin")
    .Build();
```

Save and load whenever you want a checkpoint:

```csharp
world.Save();
world.Load();
```

`AddBinaryPersistence("save.bin")` sets `World.DefaultPersistenceStore` to that file, so a bare `Save()`/`Load()` targets it automatically. Pass a path or an `IPersistenceStore` explicitly to target a different file for that one call, without changing the default:

```csharp
world.Save("saves/other.bin");
world.Load("saves/other.bin");
```

### JSON

Reference `Wyrd.Ecs.Persistence.Json` instead. Every `IComponent` is included by default, no attribute required:

```csharp
var world = new WorldBuilder()
    .AddJsonPersistence("save.json")
    .Build();
```

Opt a specific component out with `[JsonPersistenceIgnore]`:

```csharp
[JsonPersistenceIgnore]
public struct Transient : IComponent { }
```

:::note
Nothing stops a `World` from having both a binary and a JSON store configured at once, their codec registries stay independent if you do. Most worlds want one codec though, this is the unusual case, not the norm.
:::

## Then, optionally: continuous persistence

For crash-safe incremental saves, layer `Wyrd.Ecs.Persistence.Continuous` on top of whichever codec you picked:

```csharp
var world = new WorldBuilder()
    .AddBinaryPersistence("save.bin")
    .EnableContinuousPersistence()
    .Build();
```

This writes a bootstrap checkpoint, then starts a background WAL writer that captures every tracked change, plus a checkpoint-merge thread that periodically folds the WAL back in.

The WAL lands next to the checkpoint by default, `save.bin.wal.<tick>` segment files in the same directory, inferred from `DefaultPersistenceStore` since it's a `FileStore`. Pass `walStore` to `EnableContinuousPersistence` to put it somewhere else, or if the store isn't a `FileStore` at all, where it's required.

If the process exits without calling `StopContinuousPersistence`, a safety net stops and merges it for you. Call it yourself for a clean shutdown:

```csharp
world.StopContinuousPersistence();
```

## How it fits together

- `world.Save()`/`Load()` are the manual, on-demand primitive: a synchronous walk of every entity and every registered component, through an `IPersistenceStore`. Continuous persistence's periodic checkpoint calls the same `Save`, it doesn't duplicate the walk.
- `IPersistenceStore` is where the bytes go. `FileStore` (a single local file, written atomically) is the only implementation today.
- `ComponentCodecRegistry` is what makes a component type persistable at all. The binary and JSON packages populate one for you from your own component types, you only touch it directly if you're writing a custom codec.
