using SDL3;
using Wyrd.Ecs.Assets;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class RendererSystemMeshesTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddWindow("Renderer Mesh Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();

    [Fact]
    public async Task LoadModel_SingleMaterialCube_ResolvesToOnePartLoaded()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();

        var loadTask = renderer.LoadModelAsync(FixturePath("cube.obj"));
        for (var i = 0; i < 50 && !loadTask.IsCompleted; i++)
        {
            world.Update(TimeSpan.FromMilliseconds(16));
            await Task.Delay(10);
        }

        var parts = await loadTask;
        parts.Should().ContainSingle();
        renderer.GetMeshLoadState(parts[0].Mesh).Should().Be(LoadState.Loaded);
    }

    [Fact]
    public async Task LoadModel_MultiMaterialCube_ResolvesToTwoPartsEachWithTexture()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();

        var loadTask = renderer.LoadModelAsync(FixturePath("cube-multimaterial.obj"));
        for (var i = 0; i < 50 && !loadTask.IsCompleted; i++)
        {
            world.Update(TimeSpan.FromMilliseconds(16));
            await Task.Delay(10);
        }

        var parts = await loadTask;
        parts.Should().HaveCount(2);
        parts.Should().AllSatisfy(p => p.Texture.Should().NotBeNull());
    }

    [Fact]
    public async Task LoadModel_MissingFile_Throws()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();

        var loadTask = renderer.LoadModelAsync(FixturePath("does-not-exist.obj"));
        for (var i = 0; i < 50 && !loadTask.IsCompleted; i++)
        {
            world.Update(TimeSpan.FromMilliseconds(16));
            await Task.Delay(10);
        }

        var act = async () => await loadTask;
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task LoadModel_SamePathTwice_ParsesOnlyOnce()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();

        var firstTask = renderer.LoadModelAsync(FixturePath("cube.obj"));
        var firstParts = await AwaitParts(world, firstTask);

        var secondTask = renderer.LoadModelAsync(FixturePath("cube.obj"));
        var secondParts = await AwaitParts(world, secondTask);

        secondParts.Should().ContainSingle();
        secondParts[0].Mesh.Should().Be(firstParts[0].Mesh);
    }

    private static async Task<IReadOnlyList<RendererSystem.ModelPart>> AwaitParts(World world, Task<IReadOnlyList<RendererSystem.ModelPart>> task)
    {
        for (var i = 0; i < 50 && !task.IsCompleted; i++)
        {
            world.Update(TimeSpan.FromMilliseconds(16));
            await Task.Delay(10);
        }
        return await task;
    }
}
