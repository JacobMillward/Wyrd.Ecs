using System.Numerics;

namespace Wyrd.Ecs.Tests;

public class TeleportTests
{
    [Fact]
    public void Teleport_WritesTheGivenTransform()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().AddTransform(Transform.Identity);
        world.ApplyCommands();
        var value = new Transform { Position = new Vector3(9, 8, 7), Rotation = Quaternion.Identity, Scale = Vector3.One };

        world.Teleport(entity.Entity, value);

        world.GetComponent<Transform>(entity.Entity).Position.Should().Be(value.Position);
    }

    [Fact]
    public void Teleport_OnADynamicEntity_SnapsPreviousTransformToMatchToo()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().AddTransform(Transform.Identity);
        world.ApplyCommands();
        var value = new Transform { Position = new Vector3(9, 8, 7), Rotation = Quaternion.Identity, Scale = Vector3.One };

        world.Teleport(entity.Entity, value);

        var previous = world.GetComponent<PreviousTransform>(entity.Entity);
        previous.Position.Should().Be(value.Position);
        previous.Rotation.Should().Be(value.Rotation);
        previous.Scale.Should().Be(value.Scale);
    }

    [Fact]
    public void Teleport_OnAnInterpolatedFalseEntity_LeavesItWithoutAPreviousTransform()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().AddTransform(Transform.Identity, isInterpolated: false);
        world.ApplyCommands();
        var value = new Transform { Position = new Vector3(9, 8, 7), Rotation = Quaternion.Identity, Scale = Vector3.One };

        world.Teleport(entity.Entity, value);

        world.HasComponent<PreviousTransform>(entity.Entity).Should().BeFalse();
    }

    [Fact]
    public void Teleport_ThenGetInterpolatedWorldTransform_RendersTheNewValueExactlyWithNoBlend()
    {
        var builder = new WorldBuilder().WithFixedTimestep(TimeSpan.FromSeconds(1));
        builder.AddSystem<MovesTransformEachFixedStep>();
        var world = builder.AddTransformSystem().Build();
        var entity = world.Commands.CreateEntity().AddTransform(Transform.Identity);
        world.ApplyCommands();

        // Two fixed steps land Previous=(1,0,0), Transform=(2,0,0), alpha=0.5, so an
        // un-teleported read here would blend to (1.5,0,0) instead of the teleported value.
        world.Update(TimeSpan.FromSeconds(2.5));
        var value = new Transform { Position = new Vector3(100, 0, 0), Rotation = Quaternion.Identity, Scale = Vector3.One };

        world.Teleport(entity.Entity, value);

        world.GetInterpolatedWorldTransform(entity.Entity).Position.Should().Be(value.Position);
    }
}
