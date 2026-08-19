using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class RendererSystemDrawTests
{
    [Fact]
    public void Update_WithCameraAndSprite_DoesNotThrow()
    {
        var world = new WorldBuilder()
            .AddPlatform("Renderer Draw Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();

        var cameraEntity = world.Commands.CreateEntity();
        world.Commands.AddComponent(cameraEntity, Wyrd.Ecs.Transform.Identity);
        world.Commands.AddComponent(cameraEntity, new Camera(0, ProjectionMode.Orthographic, true, 10f, 0.1f, 100f));

        var spriteEntity = world.Commands.CreateEntity();
        world.Commands.AddComponent(spriteEntity, Wyrd.Ecs.Transform.Identity);
        world.Commands.AddComponent(spriteEntity, new Sprite(SourceRect: null, Tint: Color.White));
        world.Commands.AddComponent(spriteEntity, new Material(ShaderKind.UnlitSprite, Texture: null)); // null texture -> draws the placeholder
        world.ApplyCommands();

        var act = () =>
        {
            for (var i = 0; i < 5; i++)
                world.Update(TimeSpan.FromMilliseconds(16));
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Update_NoCameras_StillRunsWithoutThrowing()
    {
        var world = new WorldBuilder()
            .AddPlatform("Renderer Draw No Camera Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();

        var act = () => world.Update(TimeSpan.FromMilliseconds(16));

        act.Should().NotThrow();
    }

    [Fact]
    public void Update_TwoCamerasDifferentClearOnBegin_DoesNotThrow()
    {
        // Exercises the spec's headline multi-camera scenario (3D world + 2D HUD overlay):
        // Order=0 clears, Order=1 layers on top without clearing. This only checks the
        // per-camera render-pass sequencing doesn't throw/assert (SDL_GPU's debug validation
        // layer is the real check here, since debugMode:true is set on the device). It does
        // not verify pixel output.
        var world = new WorldBuilder()
            .AddPlatform("Renderer Draw Multi-Camera Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();

        var worldCamera = world.Commands.CreateEntity();
        world.Commands.AddComponent(worldCamera, Wyrd.Ecs.Transform.Identity);
        world.Commands.AddComponent(worldCamera, new Camera(Order: 0, ProjectionMode.Orthographic, ClearOnBegin: true, 10f, 0.1f, 100f));

        var hudCamera = world.Commands.CreateEntity();
        world.Commands.AddComponent(hudCamera, Wyrd.Ecs.Transform.Identity);
        world.Commands.AddComponent(hudCamera, new Camera(Order: 1, ProjectionMode.Orthographic, ClearOnBegin: false, 10f, 0.1f, 100f));

        var spriteEntity = world.Commands.CreateEntity();
        world.Commands.AddComponent(spriteEntity, Wyrd.Ecs.Transform.Identity);
        world.Commands.AddComponent(spriteEntity, new Sprite(SourceRect: null, Tint: Color.White));
        world.Commands.AddComponent(spriteEntity, new Material(ShaderKind.UnlitSprite, Texture: null));
        world.ApplyCommands();

        var act = () =>
        {
            for (var i = 0; i < 5; i++)
                world.Update(TimeSpan.FromMilliseconds(16));
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Update_WithPerspectiveCameraAndMeshRenderer_DoesNotThrow()
    {
        var world = new WorldBuilder()
            .AddPlatform("Renderer Mesh Draw Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();

        var cameraEntity = world.Commands.CreateEntity();
        world.Commands.AddComponent(cameraEntity, new Wyrd.Ecs.Transform { Position = new System.Numerics.Vector3(0, 0, -5), Rotation = System.Numerics.Quaternion.Identity, Scale = System.Numerics.Vector3.One });
        world.Commands.AddComponent(cameraEntity, new Camera(0, ProjectionMode.Perspective, true, MathF.PI / 4f, 0.1f, 100f));

        var meshEntity = world.Commands.CreateEntity();
        world.Commands.AddComponent(meshEntity, Wyrd.Ecs.Transform.Identity);
        world.Commands.AddComponent(meshEntity, new MeshRenderer(default, Color.White)); // default handle -> placeholder mesh
        world.Commands.AddComponent(meshEntity, new Material(ShaderKind.UnlitMesh, Texture: null));
        world.ApplyCommands();

        var act = () =>
        {
            for (var i = 0; i < 5; i++)
                world.Update(TimeSpan.FromMilliseconds(16));
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Update_ThreeDWorldPlusTwoDHud_DoesNotThrow()
    {
        // Order=0 Perspective world (meshes), Order=1 Orthographic HUD overlay (sprites)
        // layered on top without clearing. Only checks render-pass sequencing doesn't
        // throw/assert; does not verify pixel output.
        var world = new WorldBuilder()
            .AddPlatform("Renderer 3D+HUD Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();

        var worldCamera = world.Commands.CreateEntity();
        world.Commands.AddComponent(worldCamera, new Wyrd.Ecs.Transform { Position = new System.Numerics.Vector3(0, 0, -5), Rotation = System.Numerics.Quaternion.Identity, Scale = System.Numerics.Vector3.One });
        world.Commands.AddComponent(worldCamera, new Camera(Order: 0, ProjectionMode.Perspective, ClearOnBegin: true, MathF.PI / 4f, 0.1f, 100f));

        var hudCamera = world.Commands.CreateEntity();
        world.Commands.AddComponent(hudCamera, Wyrd.Ecs.Transform.Identity);
        world.Commands.AddComponent(hudCamera, new Camera(Order: 1, ProjectionMode.Orthographic, ClearOnBegin: false, 10f, 0.1f, 100f));

        var meshEntity = world.Commands.CreateEntity();
        world.Commands.AddComponent(meshEntity, Wyrd.Ecs.Transform.Identity);
        world.Commands.AddComponent(meshEntity, new MeshRenderer(default, Color.White));
        world.Commands.AddComponent(meshEntity, new Material(ShaderKind.UnlitMesh, Texture: null));

        var spriteEntity = world.Commands.CreateEntity();
        world.Commands.AddComponent(spriteEntity, Wyrd.Ecs.Transform.Identity);
        world.Commands.AddComponent(spriteEntity, new Sprite(SourceRect: null, Tint: Color.White));
        world.Commands.AddComponent(spriteEntity, new Material(ShaderKind.UnlitSprite, Texture: null));
        world.ApplyCommands();

        var act = () =>
        {
            for (var i = 0; i < 5; i++)
                world.Update(TimeSpan.FromMilliseconds(16));
        };

        act.Should().NotThrow();
    }
}
