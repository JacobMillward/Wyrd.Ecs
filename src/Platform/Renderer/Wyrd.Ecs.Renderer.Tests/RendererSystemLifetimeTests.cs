using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class RendererSystemLifetimeTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddPlatform("Renderer Lifetime Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();

    [Fact]
    public void LoadTexture_AfterRemoveSystem_ThrowsObjectDisposed()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();

        world.RemoveSystem(renderer);

        var act = () => renderer.LoadTexture("does-not-matter.png");

        act.Should().Throw<ObjectDisposedException>();
    }
}
