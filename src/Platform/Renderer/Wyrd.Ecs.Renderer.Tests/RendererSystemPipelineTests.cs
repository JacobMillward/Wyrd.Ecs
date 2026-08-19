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
            .AddPlatform("Renderer Pipeline Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();
        var renderer = world.GetSystem<RendererSystem>();

        renderer.SpritePipeline.Should().NotBe(IntPtr.Zero);
        renderer.SpriteSampler.Should().NotBe(IntPtr.Zero);
    }
}
