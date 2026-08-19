using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class WorldBuilderRendererExtensionsTests
{
    [Fact]
    public void AddRenderer_RegistersARendererSystemBoundToThePlatformSystem()
    {
        var world = new WorldBuilder()
            .AddPlatform("Renderer Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();

        var renderer = world.GetSystem<RendererSystem>();

        renderer.Device.Should().NotBe(IntPtr.Zero);
    }
}
