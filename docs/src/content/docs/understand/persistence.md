---
title: Persistence
description: The full-world walk underneath Save and Load, and how continuous persistence's WAL relates to it.
---

[Persistence](/build/persistence/) covers picking a codec and turning it on. This is what `Save()` and `Load()` actually do, and how the WAL from continuous persistence fits in.

## Save is one walk, not a per-tick log

`World.EnumerateAll` walks every entity and yields one encoded value per entity per registered component type, that's the entire save: a synchronous, full-world pass, not an incrementally maintained log. `EnumerateRelations` does the same for relation edges. Neither is meant to run every tick, a save is something you call when you actually want a checkpoint.

## CodecRegistry decouples the walk from the format

The walk itself doesn't know binary from JSON, it just yields typed values through whatever's registered on `CodecRegistry`. `AddBinaryPersistence`/`AddJsonPersistence` populate that registry for the common case, registering every `IComponent`/`ITag` by default, but the registry is the actual seam: a hand-written codec, a manual `RegisterTag`, or a migration step all plug into the same registry the walk reads from, regardless of which packaged codec built it.

## Continuous persistence reuses the walk once, then stops

`EnableContinuousPersistence` writes one bootstrap checkpoint using the same full-world walk `Save()` uses, then switches to a background WAL writer that captures changes incrementally instead of walking the whole world again, through the same subscription mechanism [Change Tracking](/understand/change-tracking/) exposes directly: it subscribes to every codec's type and drains each tick. The periodic checkpoint-merge thread never touches a live `World` or repeats that walk: it reads the previous checkpoint into memory, replays every WAL record since it over that in-memory copy, and writes the merged result back through the same atomic `IPersistenceStore` path `Save` uses. That's why the WAL only has to cover the gap since the last checkpoint, not the world's whole history, and why merging can run entirely out of band with no synchronization against a live sim thread.

## Next

[Source Generation](/understand/source-generation/) covers what makes each component serializable without a hand-written serializer.
