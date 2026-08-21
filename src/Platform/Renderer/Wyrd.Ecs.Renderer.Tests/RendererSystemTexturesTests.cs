using SDL3;
using StbImageWriteSharp;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class RendererSystemTexturesTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddWindow("Renderer Texture Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();

    private static string WriteTinyTestPng()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wyrd-test-{Guid.NewGuid():N}.png");
        var image = new ImageWriter();
        var pixels = new byte[2 * 2 * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 255;
            pixels[i + 1] = 0;
            pixels[i + 2] = 0;
            pixels[i + 3] = 255;
        }
        using var stream = File.OpenWrite(path);
        image.WritePng(pixels, 2, 2, ColorComponents.RedGreenBlueAlpha, stream);
        return path;
    }

    [Fact]
    public async Task LoadTexture_ValidPng_ResolvesToLoaded()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();
        var path = WriteTinyTestPng();

        var handle = renderer.LoadTexture(path);
        var loadTask = renderer.WaitForLoadAsync(handle);

        for (var i = 0; i < 50 && !loadTask.IsCompleted; i++)
        {
            world.Update(TimeSpan.FromMilliseconds(16));
            await Task.Delay(10);
        }

        await loadTask;
        renderer.GetTextureLoadState(handle).Should().Be(LoadState.Loaded);

        File.Delete(path);
    }

    [Fact]
    public async Task LoadTexture_MissingFile_ResolvesToFailed()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();

        var handle = renderer.LoadTexture("does/not/exist.png");
        var loadTask = renderer.WaitForLoadAsync(handle);

        for (var i = 0; i < 50 && !loadTask.IsCompleted; i++)
        {
            world.Update(TimeSpan.FromMilliseconds(16));
            await Task.Delay(10);
        }

        var act = async () => await loadTask;
        await act.Should().ThrowAsync<Exception>();
        renderer.GetTextureLoadState(handle).Should().Be(LoadState.Failed);
    }

    [Fact]
    public async Task LoadTexture_SamePathTwice_DecodesOnlyOnce()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();
        var path = WriteTinyTestPng();

        var first = renderer.LoadTexture(path);
        var second = renderer.LoadTexture(path);

        var loadTask = renderer.WaitForLoadAsync(first);
        for (var i = 0; i < 50 && !loadTask.IsCompleted; i++)
        {
            world.Update(TimeSpan.FromMilliseconds(16));
            await Task.Delay(10);
        }
        await loadTask;

        second.Should().Be(first);
        renderer.GetTextureLoadState(second).Should().Be(LoadState.Loaded);

        File.Delete(path);
    }
}
