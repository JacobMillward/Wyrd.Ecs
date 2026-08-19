using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class RendererSystemPlaceholderTests
{
    [Fact]
    public void PlaceholderTexture_IsCreatedEagerly()
    {
        var world = new WorldBuilder()
            .AddPlatform("Renderer Placeholder Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();
        var renderer = world.GetSystem<RendererSystem>();

        renderer.PlaceholderTexture.Should().NotBeNull();
        renderer.PlaceholderTexture.GpuTexture.Should().NotBe(IntPtr.Zero);
        renderer.PlaceholderTexture.PixelWidth.Should().Be(2);
        renderer.PlaceholderTexture.PixelHeight.Should().Be(2);
    }
}
