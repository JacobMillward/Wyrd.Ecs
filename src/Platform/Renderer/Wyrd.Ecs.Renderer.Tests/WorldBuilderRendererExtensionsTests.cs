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
            .AddWindow("Renderer Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();

        var renderer = world.GetSystem<RendererSystem>();

        renderer.Device.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void AddRenderer_CalledBeforeAddWindow_StillResolvesTheRealPlatformSystem()
    {
        var world = new WorldBuilder()
            .AddRenderer()
            .AddWindow("Renderer Order-Independence Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .Build();

        var renderer = world.GetSystem<RendererSystem>();

        renderer.Device.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void AddRenderer_WithNoAddWindowInTheChain_ThrowsNamingPlatformSystem()
    {
        var builder = new WorldBuilder().AddRenderer();

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*RendererSystem*PlatformSystem*");
    }
}
