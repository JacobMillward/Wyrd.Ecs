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

    [Fact]
    public void EntityTemplate_AddTransform_AddsBothTransformAndAMatchingPreviousTransform()
    {
        var world = new World();
        var value = new Transform { Position = new Vector3(1, 2, 3), Rotation = Quaternion.Identity, Scale = Vector3.One };
        var template = new EntityTemplate().AddTransform(value);

        var entity = world.Commands.CreateEntity(template);
        world.ApplyCommands();

        world.GetComponent<Transform>(entity.Entity).Position.Should().Be(value.Position);
        var previous = world.GetComponent<PreviousTransform>(entity.Entity);
        previous.Position.Should().Be(value.Position);
        previous.Rotation.Should().Be(value.Rotation);
        previous.Scale.Should().Be(value.Scale);
    }

    [Fact]
    public void AddTransform_IsStaticTrue_AddsOnlyTransform()
    {
        var world = new World();
        var value = new Transform { Position = new Vector3(1, 2, 3), Rotation = Quaternion.Identity, Scale = Vector3.One };

        var entity = world.Commands.CreateEntity().AddTransform(value, isStatic: true);
        world.ApplyCommands();

        world.GetComponent<Transform>(entity.Entity).Position.Should().Be(value.Position);
        world.HasComponent<PreviousTransform>(entity.Entity).Should().BeFalse();
    }

    [Fact]
    public void EntityTemplate_AddTransform_IsStaticTrue_AddsOnlyTransform()
    {
        var world = new World();
        var value = new Transform { Position = new Vector3(1, 2, 3), Rotation = Quaternion.Identity, Scale = Vector3.One };
        var template = new EntityTemplate().AddTransform(value, isStatic: true);

        var entity = world.Commands.CreateEntity(template);
        world.ApplyCommands();

        world.GetComponent<Transform>(entity.Entity).Position.Should().Be(value.Position);
        world.HasComponent<PreviousTransform>(entity.Entity).Should().BeFalse();
    }
}
