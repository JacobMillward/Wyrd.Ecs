using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class RendererSystemLifetimeTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddWindow("Renderer Lifetime Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
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

    [Fact]
    public async Task WaitForLoadAsync_TornDownWhileLoadInFlight_FaultsInsteadOfHangingForever()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();
        var handle = renderer.LoadTexture("does-not-matter.png");
        var waitTask = renderer.WaitForLoadAsync(handle);

        // No await/yield between LoadTexture and RemoveSystem, the background Task.Run decode
        // gets no opportunity to run first, so the load is still genuinely in flight here.
        world.RemoveSystem(renderer);

        var act = async () => await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
