---
title: Custom Rendering
description: Where Wyrd.Ecs.Renderer's public surface stops guarding you from SDL_GPU, and how to reach past it.
---

`Wyrd.Ecs.Renderer` deliberately doesn't hide the native library underneath it. The common path ([Renderer](/build/rendering/), `Sprite`/`MeshRenderer`, `LoadTexture`/`LoadModel`) covers batched 2D and 3D drawing, when it doesn't cover what you need, the pieces underneath are public, not sealed off.

## The SDL_GPU device

```csharp
var renderer = world.GetSystem<RendererSystem>();
IntPtr device = renderer.Device;
```

`RendererSystem.Device` is the raw `SDL_GPUDevice*`, the same handle every built-in draw call uses. A custom render pass, a compute pipeline, anything SDL3-CS exposes on a GPU device is reachable from here, `RendererSystem` doesn't wrap or restrict it.

## Handles are an arena reference, not the asset

```csharp
public readonly record struct Handle<T>(int Index, int Generation);
```

`Handle<Texture>`/`Handle<Mesh>` are cheap-to-copy indices into an internal, path-keyed arena. Loading the same path twice returns the same handle with its use-count bumped, not a duplicate upload. `Generation` catches use-after-unload: a slot reused by a later load gets a new generation, so a stale handle from before the reuse compares unequal instead of silently resolving to whatever asset took its place.

:::note
The arenas themselves are internal, not part of the public API. The handle, use-count, and generation behavior above is what a caller actually interacts with.
:::

## Shaders are a name, not a pipeline selector yet

```csharp
public readonly record struct ShaderKind(string Name);
```

`ShaderKind.UnlitSprite` and `ShaderKind.UnlitMesh` are the two pipelines this package ships, and `Material` compares `ShaderKind`s by `Name` for batching, two entities need the same shader (by name) and the same texture to share a draw call.

:::caution
A custom `ShaderKind` value changes batching grouping only. The sprite and mesh draw paths always bind the built-in `SpritePipeline`/`MeshPipeline`, there's no dispatch from `ShaderKind` to a pipeline you supply. A genuinely custom shader means building your own render pass against `Device` above.
:::

## Next

[Renderer](/build/rendering/) covers the common path this page assumes you've already outgrown.
