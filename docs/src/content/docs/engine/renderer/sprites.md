---
title: Sprites
description: The 2D drawable component, spritesheets, and loading textures.
---

A 2D drawable entity needs `Transform`, `Sprite`, and `Material` together.

```csharp
var renderer = world.GetSystem<RendererSystem>();
var handle = renderer.LoadTexture("assets/hero.png");

var sprite = world.Commands.CreateEntity();
world.Commands.AddComponent(sprite, Transform.Identity);
world.Commands.AddComponent(sprite, new Sprite(SourceRect: null, Tint: Color.White));
world.Commands.AddComponent(sprite, new Material(ShaderKind.UnlitSprite, handle));
world.ApplyCommands();
```

`SourceRect` is `null` for the whole texture, or a pixel-space `Rect` for one frame of a spritesheet. `Tint` multiplies the texture's own colors, `Color.White` leaves them unchanged.

:::note
`Tint` lives on `Sprite`, not `Material`. Two sprites sharing one `Material` with different tints still batch into the same draw call, only the shader and texture have to match.
:::

## Loading textures

```csharp
var handle = renderer.LoadTexture("assets/hero.png");
await renderer.WaitForLoad(handle);
```

`LoadTexture` reserves a `Handle<Texture>` immediately and decodes/uploads in the background. A sprite entity can reference the handle before the texture finishes, `RendererSystem` draws a magenta/black checkerboard in its place until it's loaded, so a broken or still-loading texture looks wrong on screen instead of silently disappearing. `WaitForLoad` is there for when you actually need to know it's ready, most entities don't need to await it at all.

```csharp
renderer.Unload(handle);
```

Drops the texture's use-count, once nothing references it, the GPU texture releases a few frames later, never while a frame in flight might still read it.

## Next

[Meshes](/engine/renderer/meshes/) for the 3D path.
