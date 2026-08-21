using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class RenderAssetsTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddWindow("RenderAssets Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();

    [Fact]
    public void AddRenderer_RegistersRenderAssetsResource()
    {
        var world = BuildWorldWithPlatform();

        var assets = world.GetResource<RenderAssets>();

        assets.Renderer.Should().BeSameAs(world.GetSystem<RendererSystem>());
    }

    [Fact]
    public void LoadTexture_SamePathViaRenderAssetsAndRendererSystem_DedupesToSameHandle()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();
        var assets = world.GetResource<RenderAssets>();
        // Reserve() is a synchronous, path-string-keyed dedup step, run before any real file
        // access happens in the background decode Task, so this doesn't need a real image file.
        const string path = "does-not-need-to-exist.png";

        var viaRenderer = renderer.LoadTexture(path);
        var viaRenderAssets = assets.LoadTexture(path);

        viaRenderAssets.Should().Be(viaRenderer, "the arena dedupes by path regardless of which entry point loaded it first");
    }
}
