using System.Linq;
using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class RendererSystemPipelineTests
{
    [Fact]
    public void AllKnownShaderKindAndBlendModeCombinations_AreCreatedEagerly()
    {
        var world = new WorldBuilder()
            .AddWindow("Renderer Pipeline Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();
        var renderer = world.GetSystem<RendererSystem>();

        // 2 ShaderKinds (UnlitSprite, UnlitMesh) x 2 BlendModes (Opaque, Transparent) = 4,
        // all created by the constructor. No draw call happens before this assertion.
        renderer.PipelineCount.Should().Be(4);
    }

    [Theory]
    [InlineData(BlendMode.Opaque)]
    [InlineData(BlendMode.Transparent)]
    public void GetOrCreatePipeline_SameKeyTwice_ReturnsSamePipeline(BlendMode blendMode)
    {
        var world = new WorldBuilder()
            .AddWindow("Renderer Pipeline Cache Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();
        var renderer = world.GetSystem<RendererSystem>();
        var key = new PipelineKey(ShaderKind.UnlitSprite, blendMode);

        var first = renderer.GetOrCreatePipeline(key);
        var second = renderer.GetOrCreatePipeline(key);

        second.Should().Be(first);
        first.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void GetOrCreatePipeline_DifferentShaderKinds_ReturnDifferentPipelines()
    {
        var world = new WorldBuilder()
            .AddWindow("Renderer Pipeline Distinct Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();
        var renderer = world.GetSystem<RendererSystem>();

        var sprite = renderer.GetOrCreatePipeline(new PipelineKey(ShaderKind.UnlitSprite, BlendMode.Opaque));
        var mesh = renderer.GetOrCreatePipeline(new PipelineKey(ShaderKind.UnlitMesh, BlendMode.Opaque));

        sprite.Should().NotBe(mesh);
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
