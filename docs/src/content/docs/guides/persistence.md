---
title: Persistence
description: Saving and loading world state, from a single save file to crash-safe incremental checkpoints.
---

If you want to persist world state to disk, Wyrd treats it as a package away, not a system you build yourself. Pick a codec, binary or JSON, then optionally layer continuous persistence on top.

## Pick a codec

Binary and JSON share one DX: reference the package, every `IComponent` is included by default, no attribute required.

```csharp
public struct Position : IComponent
{
    public float X;
    public float Y;
}
```

Opt a specific component out with `[PersistenceIgnore]`, identically for either codec:

```csharp
[PersistenceIgnore]
public struct Transient : IComponent { }
```

What differs between them is what the save file is *for*:

- **Binary** (`Wyrd.Ecs.Persistence.Binary`, backed by MemoryPack) is compact and the faster of the two to write and read, at the cost of being opaque bytes on disk. A component field shape too complex to generate a serializer for automatically (a build error names the field) can still be hand-annotated with `[MemoryPackable]`, following MemoryPack's own conventions for that shape.
- **JSON** (`Wyrd.Ecs.Persistence.Json`) is human-readable and diffable, worth it for save files you want to inspect, hand-edit, or check into version control as test fixtures, at the cost of being larger and slower than binary.

Reference whichever fits, then wire it up with a one-liner:

```csharp
var world = new WorldBuilder()
    .AddBinaryPersistence("save.bin") // or .AddJsonPersistence("save.json")
    .Build();
```

Save and load whenever you want a checkpoint:

```csharp
world.Save();
world.Load();
```

`AddBinaryPersistence`/`AddJsonPersistence` sets `World.DefaultPersistenceStore` to the path given, so a bare `Save()`/`Load()` targets it automatically. Pass a path or an `IPersistenceStore` explicitly to target a different file for that one call, without changing the default:

```csharp
world.Save("saves/other.bin");
world.Load("saves/other.bin");
```

:::note
`World.DefaultPersistenceStore` and `World.CodecRegistry` are each a single slot, not a list. Chaining `.AddBinaryPersistence(...)` and `.AddJsonPersistence(...)` on the same builder doesn't configure both, the second call's store and registry replace the first's. Pick one codec per `World`.
:::

## Rename-safety

A component/relation/tag's discriminator defaults to its fully qualified type name. Pin it explicitly instead with `[StableName]`:

```csharp
[StableName("Enemy")]
public partial struct EnemyController : IComponent { }
```

Renamed a type that's already been saved under its old name? `[RenamedFrom]` keeps that data resolving, repeatable for a type renamed more than once:

```csharp
[RenamedFrom("Old.Namespace.EnemyController")]
[StableName("Enemy")]
public partial struct EnemyController : IComponent { }
```

## Migration

A component's shape can change over time without losing old saves. `AddBinaryPersistence`/`AddJsonPersistence` already set `World.CodecRegistry` to the registry they built, register a step on it for each schema-hash transition; `Load` walks the chain automatically:

```csharp
world.CodecRegistry!.RegisterMigration("Enemy", fromSchemaHash: 1, toSchemaHash: 2, oldBytes => /* transform */);
```

## Tags

Tag presence is part of saved state, on by default for every `ITag` type. Opt a specific tag out with `[PersistenceIgnore]`, the same attribute components use:

```csharp
[PersistenceIgnore]
public struct Hovered : ITag { }
```

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
- `CodecRegistry` is the surface for a custom codec, a manual `RegisterTag`, or a hand-written migration/alias, the binary and JSON packages populate one automatically for the common case.
