---
title: Assets
description: The dedup, use-count, and generation-tracked loading pattern behind renderer and audio assets - reusable for loaders of your own.
---

`Wyrd.Ecs.Assets` holds one type, `AssetArena<TKey,TAsset>`, but it's the pattern every load in the engine goes through: textures, meshes, sounds all reserve slots in an arena keyed by path, share one underlying load per key, and release through use-counted handles. It ships separately because the same shape fits asset types of your own - save-game portraits, mod configs, anything slow to build that more than one system wants.

## Handles

```csharp
var arena = new AssetArena<string, Texture>();

Handle<Texture> handle = arena.Reserve("textures/goblin.png", out bool isNew);
```

A `Handle<T>` is a cheap-to-copy `(int Index, int Generation)` pair, never the asset itself. `T` is a compile-time tag only - it stops a `Handle<Texture>` being passed where a `Handle<Mesh>` is expected, and nothing at runtime inspects it.

The generation number catches use-after-unload. When a slot's last handle goes and the index gets reused by a later reserve, the reused slot has a new generation, so a stale handle from before compares unequal instead of silently resolving to someone else's texture. Access through a stale handle throws.

## Reserving and resolving

`Reserve` is dedup plus bookkeeping: first call for a key allocates a slot with a use-count of 1; every later call for the same key returns the existing handle and bumps the count. The `isNew` flag tells you which happened - when it's `false`, skip your own loading work entirely, the original reservation's load is already in flight or done.

Whoever saw `isNew == true` does the actual work (decode, file read, GPU upload), then reports back:

```csharp
try
{
    var pixels = DecodePng("textures/goblin.png");
    var resolution = arena.MarkLoaded(handle, new Texture(pixels));
    if (resolution == AssetResolution.SlotDiscarded)
        texture.Dispose(); // nobody owns this anymore, release it yourself
}
catch (Exception e)
{
    arena.MarkFailed(handle, e);
}
```

Resolution is first-wins. `Landed` means your result became the slot's truth, `AlreadyResolved` means a racing caller got there first and theirs stands. `SlotDiscarded` means the slot was unloaded while the load was still in flight - legal, a scene can switch faster than disk - and is not an error: whatever your pipeline created for the upload belongs to you to clean up, so the mark methods return instead of throwing.

## Waiting on a load

```csharp
await arena.WaitForLoadAsync(handle);
```

Every handle for the same key shares one task. Polling works too: `arena.GetState(handle)` returns `Loading`/`Loaded`/`Failed`, `TryGet(handle)` returns the resolved asset or `null` while it isn't ready. A load that fails faults every waiter with the original exception, including ones that call `WaitForLoadAsync` after the failure already happened.

## Unloading

```csharp
if (arena.Unload(handle, out Texture? readyForRelease))
{
    readyForRelease?.Dispose(); // timing of GPU release is the caller's business
}
```

`Unload` decrements the use-count. Only when the last handle for a key lets go does the slot actually go away - the arena hands the asset back through `readyForRelease` and leaves releasing it to you, since only the caller knows the right moment (frames-in-flight, decoder threads). Unloading while a load is still running faults its waiters rather than leaving them pending forever.

For teardown, `FaultAllPending(exception)` resolves every still-loading slot at once, so awaits in flight observe shutdown instead of hanging.

## Next

[Multi-Device](/build/input/multi-device/) continues the engine tour with input across more than one keyboard or mouse.
