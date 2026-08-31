---
title: Meshes
description: The 3D drawable component, loading multi-material models, and spawning them.
---

A 3D drawable entity needs `Transform`, `MeshRenderer`, and `Material` together, same shape as a [sprite entity](/build/rendering/sprites/), swapping `Sprite` for `MeshRenderer` and pointing `Material` at `ShaderKind.UnlitMesh`.

## Loading and spawning a model

```csharp
var renderer = world.GetSystem<RendererSystem>();
var parts = await renderer.LoadModel("assets/ship.obj");
var ship = world.Commands.CreateEntity(parts.ToEntityTemplate()).AddTransform(Transform.Identity);
world.ApplyCommands();
```

`LoadModel` parses the file off-thread via Assimp and reserves one `Handle<Mesh>` per sub-mesh, a multi-material model becomes multiple mesh assets from one file. `ToEntityTemplate()` turns the resolved parts into a reusable `EntityTemplate`: one parent, one child per part carrying `MeshRenderer` and `Material` (`ShaderKind.UnlitMesh`, `Color.White` tint). `CreateEntity(template).AddTransform(...)` instantiates and positions it in one chain. Destroying the parent destroys every child with it, the same [parent/child hierarchy](/build/ecs/relations/parent-child/) used everywhere else.

:::note
Every part spawns with `Color.White`. A different tint per part means mutating the returned children's `MeshRenderer` afterward, `ToEntityTemplate()` doesn't take per-part tint as a parameter.
:::

## A single drawable mesh directly

```csharp
world.Commands.AddComponent(entity, new MeshRenderer(handle, Color.White));
world.Commands.AddComponent(entity, new Material(ShaderKind.UnlitMesh, texture));
```

`MeshRenderer` pairs a `Handle<Mesh>` (one part from `LoadModel`) with a `Tint`, the same per-instance-data role `Sprite` plays for 2D. Unlike `Material`, a mesh isn't pipeline-selecting state, two different meshes can share one `Material` and still batch separately per (material, mesh) pair.

`Material`'s default `BlendMode.Opaque` is the right choice for solid geometry like this. For glass, foliage, or anything else that needs `Tint`'s alpha to show through, pass `BlendMode.Transparent`, see [BlendMode](/build/rendering/#blendmode).

## Unloading

```csharp
renderer.Unload(handle);
```

Same use-count/deferred-release behavior as [`Unload(Handle<Texture>)`](/build/rendering/sprites/#loading-textures), called once per `Handle<Mesh>` a part returned.

## Next

Both drawable paths need a camera, see [Renderer](/build/rendering/#cameras) if you skipped it. For direct SDL_GPU access when the common path isn't enough, see [Custom Rendering](/build/rendering/custom-rendering/).
