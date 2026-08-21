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

    [Fact]
    public void GetInterpolatedWorldTransform_StaticEntityNoParent_ReturnsItsCurrentValueWithoutThrowing()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity().AddTransform(new Vector3(3, 4, 5), isInterpolated: false);
        world.ApplyCommands();

        var act = () => world.GetInterpolatedWorldTransform(entity);

        act.Should().NotThrow();
        var interpolated = act();
        interpolated.Position.Should().Be(new Vector3(3, 4, 5));
        interpolated.Rotation.Should().Be(Quaternion.Identity);
        interpolated.Scale.Should().Be(Vector3.One);
    }

    [Fact]
    public void GetInterpolatedWorldTransform_StaticChildOfAMovingDynamicParent_OnlyTheParentLinkInterpolates()
    {
        var builder = new WorldBuilder().WithFixedTimestep(TimeSpan.FromSeconds(1));
        builder.AddSystem<MovesDynamicTransformEachFixedStep>();
        var world = builder.AddTransformSystem().Build();

        var parent = world.Commands.CreateEntity().AddTransform(Transform.Identity);
        var child = world.Commands.CreateEntity().AddTransform(new Vector3(1, 0, 0), isInterpolated: false);
        child.SetParent(parent.Entity);
        world.ApplyCommands();

        // Same accumulator math as GetInterpolatedWorldTransform_BlendsBetweenPreviousAndCurrent:
        // two fixed steps land the parent's Previous=(1,0,0), current=(2,0,0), alpha=0.5.
        world.Update(TimeSpan.FromSeconds(2.5));

        var interpolated = world.GetInterpolatedWorldTransform(child.Entity);

        // Parent contributes the interpolated (1.5,0,0); child's own local (1,0,0) is exact,
        // since a static entity has no Previous to blend against.
        interpolated.Position.Should().Be(new Vector3(2.5f, 0, 0));
    }

    [Fact]
    public void GetInterpolatedWorldTransform_DynamicChildOfAStaticParent_OnlyTheChildLinkInterpolates()
    {
        var builder = new WorldBuilder().WithFixedTimestep(TimeSpan.FromSeconds(1));
        builder.AddSystem<MovesDynamicTransformEachFixedStep>();
        var world = builder.AddTransformSystem().Build();

        var parent = world.Commands.CreateEntity().AddTransform(new Vector3(10, 0, 0), isInterpolated: false);
        var child = world.Commands.CreateEntity().AddTransform(Transform.Identity);
        child.SetParent(parent.Entity);
        world.ApplyCommands();

        world.Update(TimeSpan.FromSeconds(2.5));

        var interpolated = world.GetInterpolatedWorldTransform(child.Entity);

        // Parent's static (10,0,0) is exact; child's own interpolated local is (1.5,0,0).
        interpolated.Position.Should().Be(new Vector3(11.5f, 0, 0));
    }

    [Fact]
    public void ToWorldPoint_WithIdentityTransform_ReturnsTheSamePoint()
    {
        var worldTransform = new WorldTransform(Vector3.Zero, Quaternion.Identity, Vector3.One);

        worldTransform.ToWorldPoint(new Vector3(1, 2, 3)).Should().Be(new Vector3(1, 2, 3));
    }

    [Fact]
    public void ToWorldPoint_AtLocalOrigin_ReturnsThisWorldTransformsOwnPosition()
    {
        var worldTransform = new WorldTransform(new Vector3(4, 5, 6), Quaternion.Identity, Vector3.One);

        worldTransform.ToWorldPoint(Vector3.Zero).Should().Be(new Vector3(4, 5, 6));
    }

    [Fact]
    public void ToWorldPoint_AppliesScaleBeforeAddingPosition()
    {
        var worldTransform = new WorldTransform(new Vector3(10, 0, 0), Quaternion.Identity, new Vector3(2, 2, 2));

        worldTransform.ToWorldPoint(new Vector3(1, 0, 0)).Should().Be(new Vector3(12, 0, 0));
    }

    [Fact]
    public void ToWorldPoint_ThenToLocalPoint_RoundTripsForAnArbitraryTransform()
    {
        var worldTransform = new WorldTransform(
            new Vector3(3, -2, 7),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 3f),
            new Vector3(2, 0.5f, 3));
        var localPoint = new Vector3(1, 2, 3);

        var roundTripped = worldTransform.ToLocalPoint(worldTransform.ToWorldPoint(localPoint));

        roundTripped.X.Should().BeApproximately(localPoint.X, 0.0001f);
        roundTripped.Y.Should().BeApproximately(localPoint.Y, 0.0001f);
        roundTripped.Z.Should().BeApproximately(localPoint.Z, 0.0001f);
    }

    [Fact]
    public void ToWorldOffset_OfZero_IsAlwaysZero()
    {
        var worldTransform = new WorldTransform(new Vector3(4, 5, 6), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4f), new Vector3(2, 2, 2));

        worldTransform.ToWorldOffset(Vector3.Zero).Should().Be(Vector3.Zero);
    }

    [Fact]
    public void ToWorldOffset_IgnoresPosition()
    {
        var atOrigin = new WorldTransform(Vector3.Zero, Quaternion.Identity, Vector3.One);
        var farFromOrigin = new WorldTransform(new Vector3(100, -50, 25), Quaternion.Identity, Vector3.One);

        atOrigin.ToWorldOffset(new Vector3(1, 0, 0)).Should().Be(farFromOrigin.ToWorldOffset(new Vector3(1, 0, 0)));
    }

    [Fact]
    public void ToWorldOffset_AppliesScale()
    {
        var worldTransform = new WorldTransform(new Vector3(10, 0, 0), Quaternion.Identity, new Vector3(2, 2, 2));

        worldTransform.ToWorldOffset(new Vector3(1, 0, 0)).Should().Be(new Vector3(2, 0, 0));
    }

    [Fact]
    public void ToWorldOffset_ThenToLocalOffset_RoundTripsForAnArbitraryTransform()
    {
        var worldTransform = new WorldTransform(
            new Vector3(3, -2, 7),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 3f),
            new Vector3(2, 0.5f, 3));
        var localOffset = new Vector3(1, 2, 3);

        var roundTripped = worldTransform.ToLocalOffset(worldTransform.ToWorldOffset(localOffset));

        roundTripped.X.Should().BeApproximately(localOffset.X, 0.0001f);
        roundTripped.Y.Should().BeApproximately(localOffset.Y, 0.0001f);
        roundTripped.Z.Should().BeApproximately(localOffset.Z, 0.0001f);
    }
}

/// <summary>
/// Like <see cref="MovesTransformEachFixedStep"/>, but scoped to dynamic entities only
/// (via <c>.Has&lt;PreviousTransform&gt;()</c>, a presence-only filter that doesn't affect
/// <c>Update</c>'s parameter list): a static entity sharing an archetype with a moving one
/// must never be moved just because it also has a <see cref="Transform"/>.
/// </summary>
[FixedTimestep]
sealed partial class MovesDynamicTransformEachFixedStep : QuerySystem
{
    protected override IQuery DefineQuery(Query query) => query.With<Transform>().Has<PreviousTransform>();

    public void Update(Time time, ref Transform transform) => transform.Position += new Vector3(1, 0, 0);
}
