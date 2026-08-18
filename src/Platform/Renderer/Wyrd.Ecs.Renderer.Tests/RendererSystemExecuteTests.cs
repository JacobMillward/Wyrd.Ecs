using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class RendererSystemExecuteTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddPlatform("Renderer Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .AddRenderer()
            .Build();

    [Fact]
    public void Update_RunsSeveralFramesWithoutThrowing()
    {
        var world = BuildWorldWithPlatform();

        var act = () =>
        {
            for (var i = 0; i < 5; i++)
                world.Update(TimeSpan.FromMilliseconds(16));
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Update_AdvancesTheFrameInFlightCounter()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();

        world.Update(TimeSpan.FromMilliseconds(16));
        world.Update(TimeSpan.FromMilliseconds(16));
        world.Update(TimeSpan.FromMilliseconds(16));

        renderer.FrameInFlight.CurrentFrame.Should().Be(3);
    }
}
