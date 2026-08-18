using System.Numerics;

namespace Wyrd.Ecs.Tests;

public class TransformSnapshotSystemTests
{
    [Fact]
    public void Update_CopiesTransformIntoPreviousTransform()
    {
        var world = new WorldBuilder()
            .WithFixedTimestep(TimeSpan.FromSeconds(1))
            .AddTransformSystem()
            .Build();
        var entity = world.Commands.CreateEntity().AddTransform(Transform.Identity with { Position = new Vector3(1, 0, 0) });
        world.ApplyCommands();

        world.Update(TimeSpan.FromSeconds(1));

        var previous = world.GetComponent<PreviousTransform>(entity.Entity);
        previous.Position.Should().Be(new Vector3(1, 0, 0));
    }
}

[FixedTimestep]
sealed partial class MovesTransformEachFixedStep : QuerySystem
{
    protected override IQuery DefineQuery(Query query) => query.With<Transform>();

    public void Update(Time time, ref Transform transform) => transform.Position += new Vector3(1, 0, 0);
}

public class TransformSnapshotOrderingTests
{
    [Fact]
    public void PreviousTransform_ReflectsTheValueBeforeThisTicksMovement_NotAfter()
    {
        // MovesTransformEachFixedStep declares no RunAfter at all, and is registered
        // before AddTransformSystem, so registration-order tie-break alone would run it
        // first. This only passes if Transform's RequiresSnapshotBefore attribute
        // actually injected the edge into the real schedule.
        // AddSystem<T>() returns SystemRegistration (for further .Before<T>()/.After<T>()
        // chaining), not WorldBuilder, so it can't chain directly into AddTransformSystem();
        // registering as two statements against the same builder instance instead.
        var builder = new WorldBuilder().WithFixedTimestep(TimeSpan.FromSeconds(1));
        builder.AddSystem<MovesTransformEachFixedStep>();
        var world = builder.AddTransformSystem().Build();
        var entity = world.Commands.CreateEntity().AddTransform(Transform.Identity);
        world.ApplyCommands();

        world.Update(TimeSpan.FromSeconds(1));

        // After one fixed step, Transform moved from (0,0,0) to (1,0,0), but the
        // snapshot must have captured (0,0,0): the value before the move.
        world.GetComponent<Transform>(entity.Entity).Position.Should().Be(new Vector3(1, 0, 0));
        world.GetComponent<PreviousTransform>(entity.Entity).Position.Should().Be(Vector3.Zero);
    }
}
