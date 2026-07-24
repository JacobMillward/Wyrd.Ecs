# Persistence

Wyrd.Ecs treats persistence as a package away, not a system you build yourself. Pick one codec package, binary or JSON, not both. Then, optionally, layer continuous persistence on top of whichever one you picked.

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

A source generator scans your project for `[MemoryPackable]` components and emits `AddBinaryPersistence`, so the builder call is a one-liner:

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

Both default to `World.DefaultPersistenceStore`; pass a path or an `IPersistenceStore` to target a specific save slot instead, e.g. for a title screen with multiple save files:

```csharp
world.Save($"saves/{slot}.bin");
world.Load($"saves/{slot}.bin");
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

(Nothing stops a `World` from having both a binary and a JSON store configured at once, and their codec registries are kept independent if you do, but that's an unusual setup, not the norm. Most worlds want one codec.)

## Then, optionally: continuous persistence

For crash-safe incremental saves, layer `Wyrd.Ecs.Persistence.Continuous` on top of whichever codec you picked:

```csharp
var world = new WorldBuilder()
    .AddBinaryPersistence("save.bin")
    .EnableContinuousPersistence()
    .Build();
```

This writes a bootstrap checkpoint, then starts a background WAL writer that captures every tracked change and a checkpoint-merge thread that periodically folds the WAL back in. If the process exits without calling `StopContinuousPersistence`, a safety net stops and merges it for you. Call it yourself for a clean shutdown:

```csharp
world.StopContinuousPersistence();
```

## How it fits together

- `world.Save()` / `Load()` are the manual, on-demand primitive: a synchronous walk of every entity and every registered component, through an `IPersistenceStore`. Continuous persistence's periodic checkpoint calls the same `Save`, it doesn't duplicate the walk.
- `IPersistenceStore` is where the bytes go. `FileStore` (a single local file, written atomically) is the only implementation today.
- `ComponentCodecRegistry` is what makes a component type persistable at all. The binary and JSON packages populate one for you from your own component types; you only touch it directly if you're writing a custom codec.
