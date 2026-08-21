using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class InstanceBufferTests
{
    [Fact]
    public void Write_WithinInitialCapacity_ReturnsNonNullBuffer_AndDoesNotGrow()
    {
        var world = new WorldBuilder()
            .AddWindow("Instance Buffer Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();
        var renderer = world.GetSystem<RendererSystem>();
        var buffer = new InstanceBuffer<SpriteInstanceData>(renderer.Device, renderer.DeferredDestroy, initialCapacity: 4);

        var commandBuffer = SDL.AcquireGPUCommandBuffer(renderer.Device);
        var copyPass = SDL.BeginGPUCopyPass(commandBuffer);
        var instances = new SpriteInstanceData[2];
        var gpuBuffer = buffer.Write(instances, currentFrame: 0, copyPass);
        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(commandBuffer);

        gpuBuffer.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void Write_ExceedingCapacity_DoublesAndStillReturnsValidBuffer()
    {
        var world = new WorldBuilder()
            .AddWindow("Instance Buffer Growth Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();
        var renderer = world.GetSystem<RendererSystem>();
        var buffer = new InstanceBuffer<SpriteInstanceData>(renderer.Device, renderer.DeferredDestroy, initialCapacity: 2);

        var commandBuffer = SDL.AcquireGPUCommandBuffer(renderer.Device);
        var copyPass = SDL.BeginGPUCopyPass(commandBuffer);
        var instances = new SpriteInstanceData[5]; // exceeds initialCapacity of 2
        var gpuBuffer = buffer.Write(instances, currentFrame: 0, copyPass);
        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(commandBuffer);

        gpuBuffer.Should().NotBe(IntPtr.Zero);
    }
}
