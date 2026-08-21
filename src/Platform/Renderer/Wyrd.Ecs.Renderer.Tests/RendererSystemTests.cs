using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class RendererSystemTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddWindow("Renderer Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();

    [Fact]
    public void Constructor_ClaimsAWorkingGPUDevice()
    {
        var world = BuildWorldWithPlatform();

        var renderer = world.GetSystem<RendererSystem>();

        renderer.Device.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void RemoveSystem_ReleasesTheDeviceWithoutThrowing()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();

        var act = () => world.RemoveSystem(renderer);

        act.Should().NotThrow();
    }
}
