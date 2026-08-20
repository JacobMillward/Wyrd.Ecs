using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class AssetLoaderTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddPlatform("AssetLoader Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();

    [Fact]
    public void AddRenderer_RegistersAssetLoaderResource()
    {
        var world = BuildWorldWithPlatform();

        var assets = world.GetResource<AssetLoader>();

        assets.Renderer.Should().BeSameAs(world.GetSystem<RendererSystem>());
    }

    [Fact]
    public void LoadTexture_SamePathViaAssetLoaderAndRendererSystem_DedupesToSameHandle()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();
        var assets = world.GetResource<AssetLoader>();
        // Reserve() is a synchronous, path-string-keyed dedup step, run before any real file
        // access happens in the background decode Task, so this doesn't need a real image file.
        const string path = "does-not-need-to-exist.png";

        var viaRenderer = renderer.LoadTexture(path);
        var viaAssetLoader = assets.LoadTexture(path);

        viaAssetLoader.Should().Be(viaRenderer, "the arena dedupes by path regardless of which entry point loaded it first");
    }
}
