---
title: Renderer
description: Draw sprites and meshes through SDL_GPU, with cameras and materials as ordinary components.
---

`Wyrd.Ecs.Renderer` draws entities through SDL_GPU: batched, frustum-culled, one draw call per (material, mesh) pair. `AddRenderer` needs `AddWindow` called somewhere in the same chain, it claims the window `AddWindow` opened - but not necessarily first, construction order doesn't matter.

## Setting up

```csharp
using Wyrd.Ecs.Renderer;

var world = new WorldBuilder()
    .AddWindow("My Game", 1280, 720)
    .AddRenderer()
    .Build();
```

## Cameras

```csharp
var camera = world.Commands.CreateEntity();
world.Commands.AddComponent(camera, Transform.Identity);
world.Commands.AddComponent(camera, new OrthographicCamera(0, true, 10f, 0.1f, 100f));
world.ApplyCommands();
```

A camera is queried as `(Transform, PerspectiveCamera)` or `(Transform, OrthographicCamera)`, an entity with no `Transform` simply never renders. Every active camera, of either kind, draws in `Order` sequence into the same swapchain target, so a 3D scene plus a 2D HUD is two camera entities, not a separate compositing pass:

```csharp
var hud = world.Commands.CreateEntity();
world.Commands.AddComponent(hud, new Transform { Position = new Vector3(0, 0, -5), Rotation = Quaternion.Identity, Scale = Vector3.One });
world.Commands.AddComponent(hud, new PerspectiveCamera(Order: 1, ClearOnBegin: false, Angle.Deg(45), 0.1f, 100f));
world.ApplyCommands();
```

`ClearOnBegin: false` draws on top of whatever the first camera already put in the swapchain, instead of erasing it first.

:::note
`PerspectiveCamera.FieldOfView` is a vertical `Angle`. `OrthographicCamera.Size` is half the vertical world-space extent.
:::

## Materials

```csharp
world.Commands.AddComponent(entity, new Material(ShaderKind.UnlitSprite, textureHandle));
```

`Material` is pipeline-selecting state only, the shader, the texture, and the blend mode, the things that have to match for two entities to batch into one draw call. Per-instance data like tint lives on the drawable component instead, see [Sprites](/build/rendering/sprites/) and [Meshes](/build/rendering/meshes/).

### BlendMode

```csharp
new Material(ShaderKind.UnlitSprite, textureHandle, BlendMode.Transparent)
```

`BlendMode.Opaque`, `Material`'s default, writes depth and tests it, draw order doesn't matter. `BlendMode.Transparent` tests depth but doesn't write it, and needs back-to-front draw order for correct compositing, the renderer sorts every transparent entity by depth for you each frame, across sprites and meshes together.

:::note
The sprite/mesh shaders premultiply each pixel's alpha before blending, so texture files stay ordinary straight-alpha PNGs.
:::

## Loading assets

Textures and meshes load through `RendererSystem`, returning a `Handle<T>`: a cheap-to-copy reference into an internal arena, not the asset itself. Loading is asynchronous, an entity can reference a `Handle<T>` before its asset finishes loading, `RendererSystem` draws a placeholder in its place until it's ready.

## Next

[Sprites](/build/rendering/sprites/) for the 2D path, [Meshes](/build/rendering/meshes/) for 3D.
