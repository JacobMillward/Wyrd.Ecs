using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer.Tests;

public class ModelPartsExtensionsTests
{
    [Fact]
    public void ToEntityTemplate_TwoParts_CreatesOneParentAndTwoChildren()
    {
        var world = new WorldBuilder().Build();
        RendererSystem.ModelPart[] parts =
        [
            new(new Handle<Mesh>(0, 0), null),
            new(new Handle<Mesh>(1, 0), null),
        ];

        var parent = world.Commands.CreateEntity(parts.ToEntityTemplate());
        world.ApplyCommands();

        world.HasComponent<MeshRenderer>(parent).Should().BeFalse();
        world.Children(parent).Should().HaveCount(2);
    }

    [Fact]
    public void ToEntityTemplate_EachChild_HasMeshRendererMatchingItsPart()
    {
        var world = new WorldBuilder().Build();
        var meshHandle = new Handle<Mesh>(0, 0);
        RendererSystem.ModelPart[] parts = [new(meshHandle, null)];

        var parent = world.Commands.CreateEntity(parts.ToEntityTemplate());
        world.ApplyCommands();

        var child = world.Children(parent).Single();
        world.GetComponent<MeshRenderer>(child).Mesh.Should().Be(meshHandle);
    }

    [Fact]
    public void ToEntityTemplate_DestroyingParent_CascadesDestroyToChildren()
    {
        var world = new WorldBuilder().Build();
        RendererSystem.ModelPart[] parts = [new(new Handle<Mesh>(0, 0), null)];
        var parent = world.Commands.CreateEntity(parts.ToEntityTemplate());
        world.ApplyCommands();
        var child = world.Children(parent).Single();

        world[parent].DestroyEntity();
        world.ApplyCommands();

        world.IsAlive(child).Should().BeFalse();
    }

    [Fact]
    public void ToEntityTemplate_PositionedViaCreateEntityThenAddTransform_LeavesTransformAndPreviousTransformMatching()
    {
        var world = new WorldBuilder().Build();
        RendererSystem.ModelPart[] parts = [new(new Handle<Mesh>(0, 0), null)];
        var spawnAt = new Transform { Position = new System.Numerics.Vector3(5, 0, 0), Rotation = System.Numerics.Quaternion.Identity, Scale = System.Numerics.Vector3.One };

        var parent = world.Commands.CreateEntity(parts.ToEntityTemplate()).AddTransform(spawnAt);
        world.ApplyCommands();

        var transform = world.GetComponent<Transform>(parent);
        var previous = world.GetComponent<PreviousTransform>(parent);
        transform.Position.Should().Be(spawnAt.Position);
        previous.Position.Should().Be(spawnAt.Position, "a bare AddComponent(root, spawnAt) would leave PreviousTransform stale at Identity, this must not");
    }
}
