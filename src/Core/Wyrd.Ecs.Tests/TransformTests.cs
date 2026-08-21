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

    [Fact]
    public void AddTransform_VectorOnly_UsesIdentityRotationAndUnitScale()
    {
        var world = new World();

        var entity = world.Commands.CreateEntity().AddTransform(new Vector3(4, 5, 6));
        world.ApplyCommands();

        var transform = world.GetComponent<Transform>(entity.Entity);
        transform.Position.Should().Be(new Vector3(4, 5, 6));
        transform.Rotation.Should().Be(Quaternion.Identity);
        transform.Scale.Should().Be(Vector3.One);
        world.HasComponent<PreviousTransform>(entity.Entity).Should().BeTrue();
    }

    [Fact]
    public void AddTransform_VectorAndQuaternion_UsesTheGivenRotationAndUnitScale()
    {
        var world = new World();
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 3f);

        var entity = world.Commands.CreateEntity().AddTransform(new Vector3(1, 0, 0), rotation);
        world.ApplyCommands();

        var transform = world.GetComponent<Transform>(entity.Entity);
        transform.Rotation.Should().Be(rotation);
        transform.Scale.Should().Be(Vector3.One);
    }

    [Fact]
    public void AddTransform_VectorAndAngle_RotatesAroundZ()
    {
        var world = new World();
        var angle = Angle.Deg(90f);

        var entity = world.Commands.CreateEntity().AddTransform(new Vector3(1, 0, 0), angle);
        world.ApplyCommands();

        var expectedRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle.Radians);
        world.GetComponent<Transform>(entity.Entity).Rotation.Should().Be(expectedRotation);
    }

    [Fact]
    public void AddTransform_VectorOnly_IsStaticTrue_AddsOnlyTransform()
    {
        var world = new World();

        var entity = world.Commands.CreateEntity().AddTransform(new Vector3(1, 0, 0), isStatic: true);
        world.ApplyCommands();

        world.HasComponent<PreviousTransform>(entity.Entity).Should().BeFalse();
    }

    [Fact]
    public void EntityTemplate_AddTransform_VectorAndAngle_RotatesAroundZ()
    {
        var world = new World();
        var angle = Angle.Deg(45f);
        var template = new EntityTemplate().AddTransform(new Vector3(0, 0, 0), angle);

        var entity = world.Commands.CreateEntity(template);
        world.ApplyCommands();

        var expectedRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle.Radians);
        world.GetComponent<Transform>(entity.Entity).Rotation.Should().Be(expectedRotation);
    }
}
