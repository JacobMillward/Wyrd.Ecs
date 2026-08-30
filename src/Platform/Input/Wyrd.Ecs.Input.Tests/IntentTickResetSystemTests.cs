using System.Numerics;

namespace Wyrd.Ecs.Input.Tests;

public class IntentTickResetSystemTests
{
    [Fact]
    public void Execute_ClearsTickFlags_ButLeavesEverythingElseAlone()
    {
        var world = new WorldBuilder().Build();
        world.AddResource(new IntentState<TestAction>());
        ref var state = ref world.GetResourceRef<IntentState<TestAction>>();
        state.States[(TestAction.Jump, default)] = new ActionState(
            IsHeld: true, JustPressed: true, JustReleased: false, Value: Vector2.UnitX,
            TickJustPressed: true, TickJustReleased: true);

        world.RunOnce(new IntentTickResetSystem<TestAction>(), TimeSpan.Zero);

        var result = world.GetResource<IntentState<TestAction>>()[TestAction.Jump];
        result.TickJustPressed.Should().BeFalse();
        result.TickJustReleased.Should().BeFalse();
        result.IsHeld.Should().BeTrue("only the tick-scoped pair is this system's job");
        result.JustPressed.Should().BeTrue();
        result.Value.Should().Be(Vector2.UnitX);
    }

    [Fact]
    public void Execute_WithNoBoundActions_DoesNothing()
    {
        var world = new WorldBuilder().Build();
        world.AddResource(new IntentState<TestAction>());

        var act = () => world.RunOnce(new IntentTickResetSystem<TestAction>(), TimeSpan.Zero);

        act.Should().NotThrow();
    }
}
