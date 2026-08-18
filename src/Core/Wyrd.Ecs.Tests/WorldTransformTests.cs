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

    [Fact]
    public void GetInterpolatedWorldTransform_BlendsBetweenPreviousAndCurrent()
    {
        // Same registration order as TransformSnapshotOrderingTests: the writer registered
        // before AddTransformSystem, so correct results here also depend on the
        // auto-injected edge, not a lucky tie-break. AddSystem<T>() returns
        // SystemRegistration, not WorldBuilder, so it can't chain directly into
        // AddTransformSystem(); registering as two statements instead.
        var builder = new WorldBuilder().WithFixedTimestep(TimeSpan.FromSeconds(1));
        builder.AddSystem<MovesTransformEachFixedStep>();
        var world = builder.AddTransformSystem().Build();
        var entity = world.Commands.CreateEntity().AddTransform(Transform.Identity);
        world.ApplyCommands();

        // One Update call with 2.5s of accumulated time drives the 1s fixed-step
        // accumulator through exactly two fixed steps, then stops with 0.5s left over.
        // Step 1: snapshot captures Previous=(0,0,0), then the move writes Transform=(1,0,0).
        // Step 2: snapshot captures Previous=(1,0,0) (this step's starting value, not the
        // original), then the move writes Transform=(2,0,0). Accumulator: 2.5 - 1 - 1 = 0.5,
        // so FixedStepAlpha == 0.5 with no third step firing.
        world.Update(TimeSpan.FromSeconds(2.5));

        var interpolated = world.GetInterpolatedWorldTransform(entity.Entity);

        // Blending Previous=(1,0,0) and Transform=(2,0,0) at alpha 0.5:
        // (1,0,0) + 0.5 * ((2,0,0) - (1,0,0)) = (1.5, 0, 0).
        interpolated.Position.Should().Be(new Vector3(1.5f, 0, 0));
    }
}
