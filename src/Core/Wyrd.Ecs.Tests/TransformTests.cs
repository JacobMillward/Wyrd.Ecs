using System.Numerics;

namespace Wyrd.Ecs.Tests;

public class TransformTests
{
    [Fact]
    public void Identity_HasZeroPositionUnitRotationAndOneScale()
    {
        Transform.Identity.Position.Should().Be(Vector3.Zero);
        Transform.Identity.Rotation.Should().Be(Quaternion.Identity);
        Transform.Identity.Scale.Should().Be(Vector3.One);
    }

    [Fact]
    public void AddTransform_AddsBothTransformAndAMatchingPreviousTransform()
    {
        var world = new World();
        var value = new Transform { Position = new Vector3(1, 2, 3), Rotation = Quaternion.Identity, Scale = Vector3.One };

        var entity = world.Commands.CreateEntity().AddTransform(value);
        world.ApplyCommands();

        world.GetComponent<Transform>(entity.Entity).Position.Should().Be(value.Position);
        var previous = world.GetComponent<PreviousTransform>(entity.Entity);
        previous.Position.Should().Be(value.Position);
        previous.Rotation.Should().Be(value.Rotation);
        previous.Scale.Should().Be(value.Scale);
    }
}
