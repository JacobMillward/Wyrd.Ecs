using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer.Tests;

[Trait("Category", "RequiresGpu")]
public class RendererSystemModelExtensionsTests
{
    private static World BuildWorldWithPlatform() =>
        new WorldBuilder()
            .AddPlatform("Renderer SpawnModel Test Window", 320, 240, SDL.WindowFlags.Hidden | SDL.WindowFlags.Vulkan)
            .AddRenderer()
            .Build();

    [Fact]
    public void SpawnModel_TwoParts_CreatesOneParentAndTwoChildren()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();
        RendererSystem.ModelPart[] parts =
        [
            new(new Handle<Mesh>(0, 0), null),
            new(new Handle<Mesh>(1, 0), null),
        ];

        var parent = renderer.SpawnModel(world, parts, Wyrd.Ecs.Transform.Identity);
        world.ApplyCommands();

        world.HasComponent<MeshRenderer>(parent).Should().BeFalse();
        world.Children(parent).Should().HaveCount(2);
    }

    [Fact]
    public void SpawnModel_EachChild_HasMeshRendererMatchingItsPart()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();
        var meshHandle = new Handle<Mesh>(0, 0);
        RendererSystem.ModelPart[] parts = [new(meshHandle, null)];

        var parent = renderer.SpawnModel(world, parts, Wyrd.Ecs.Transform.Identity);
        world.ApplyCommands();

        var child = world.Children(parent).Single();
        world.GetComponent<MeshRenderer>(child).Mesh.Should().Be(meshHandle);
    }

    [Fact]
    public void SpawnModel_DestroyingParent_CascadesDestroyToChildren()
    {
        var world = BuildWorldWithPlatform();
        var renderer = world.GetSystem<RendererSystem>();
        RendererSystem.ModelPart[] parts = [new(new Handle<Mesh>(0, 0), null)];
        var parent = renderer.SpawnModel(world, parts, Wyrd.Ecs.Transform.Identity);
        world.ApplyCommands();
        var child = world.Children(parent).Single();

        world[parent].DestroyEntity();
        world.ApplyCommands();

        world.IsAlive(child).Should().BeFalse();
    }
}
