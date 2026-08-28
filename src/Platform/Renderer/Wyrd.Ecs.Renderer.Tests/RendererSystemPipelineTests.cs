using System.Linq;
using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class RendererSystemPipelineTests
{
    [Fact]
    public void SpritePipelineAndSampler_AreCreatedEagerly()
    {
        var world = new WorldBuilder()
            .AddWindow("Renderer Pipeline Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();
        var renderer = world.GetSystem<RendererSystem>();

        renderer.SpritePipeline.Should().NotBe(IntPtr.Zero);
        renderer.SpriteSampler.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void DepthStencilFormat_MatchesHighestPrioritySupportedFormat()
    {
        var world = new WorldBuilder()
            .AddWindow("Renderer Depth Format Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();
        var renderer = world.GetSystem<RendererSystem>();

        SDL.GPUTextureFormat[] priority =
        [
            SDL.GPUTextureFormat.D32Float,
            SDL.GPUTextureFormat.D24UnormS8Uint,
            SDL.GPUTextureFormat.D16Unorm,
        ];
        var expected = priority.First(format =>
            SDL.GPUTextureSupportsFormat(renderer.Device, format, SDL.GPUTextureType.TextureType2D, SDL.GPUTextureUsageFlags.DepthStencilTarget));

        renderer.DepthStencilFormat.Should().Be(expected);
    }
}
