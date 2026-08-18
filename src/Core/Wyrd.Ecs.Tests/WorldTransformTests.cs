using System.Numerics;

namespace Wyrd.Ecs.Tests;

public class WorldTransformTests
{
    [Fact]
    public void GetWorldTransform_WithNoParent_ReturnsTheLocalTransformUnchanged()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().AddTransform(Transform.Identity with { Position = new Vector3(5, 0, 0) });
        world.ApplyCommands();

        var worldTransform = world.GetWorldTransform(entity.Entity);

        worldTransform.Position.Should().Be(new Vector3(5, 0, 0));
        worldTransform.Rotation.Should().Be(Quaternion.Identity);
        worldTransform.Scale.Should().Be(Vector3.One);
    }

    [Fact]
    public void GetWorldTransform_WithAParent_ComposesParentAndChildPosition()
    {
        var world = new World();
        var parent = world.Commands.CreateEntity().AddTransform(Transform.Identity with { Position = new Vector3(10, 0, 0) });
        var child = world.Commands.CreateEntity().AddTransform(Transform.Identity with { Position = new Vector3(1, 0, 0) });
        child.SetParent(parent.Entity);
        world.ApplyCommands();

        var worldTransform = world.GetWorldTransform(child.Entity);

        worldTransform.Position.Should().Be(new Vector3(11, 0, 0));
    }

    [Fact]
    public void GetWorldTransform_WithAParent_ComposesScaleMultiplicatively()
    {
        var world = new World();
        var parent = world.Commands.CreateEntity().AddTransform(Transform.Identity with { Scale = new Vector3(2, 2, 2) });
        var child = world.Commands.CreateEntity().AddTransform(Transform.Identity with { Position = new Vector3(1, 0, 0) });
        child.SetParent(parent.Entity);
        world.ApplyCommands();

        var worldTransform = world.GetWorldTransform(child.Entity);

        // Parent scale applies to the child's local position too: (1,0,0) * 2 = (2,0,0).
        worldTransform.Position.Should().Be(new Vector3(2, 0, 0));
        worldTransform.Scale.Should().Be(new Vector3(2, 2, 2));
    }
}
