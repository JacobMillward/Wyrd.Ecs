using System.Numerics;

namespace Wyrd.Ecs.Input.Tests;

public enum TestAction { Jump, Move }

public class IntentStateTests
{
    [Fact]
    public void ConstructedViaNew_HasAllCollectionsAllocated()
    {
        var state = new IntentState<TestAction>();

        state.TryGet(TestAction.Jump, out _).Should().BeFalse("the backing States dictionary must be allocated, not null, for TryGet to work at all");
    }

    [Fact]
    public void Indexer_ThrowsForAnActionThatWasNeverPopulated()
    {
        var state = new IntentState<TestAction>();

        var act = () => state[TestAction.Jump];

        act.Should().Throw<InvalidOperationException>().WithMessage("*Jump*");
    }

    [Fact]
    public void TryGet_ReturnsFalseForAnActionThatWasNeverPopulated()
    {
        var state = new IntentState<TestAction>();

        state.TryGet(TestAction.Jump, out var result).Should().BeFalse();
        result.Should().Be(default(ActionState));
    }

    [Fact]
    public void Indexer_ReturnsWhateverWasWrittenIntoStatesForThatActionAndProfile()
    {
        var state = new IntentState<TestAction>();
        var expected = new ActionState(true, true, false, Vector2.UnitX);
        state.States[(TestAction.Jump, new ProfileId(1))] = expected;

        state[TestAction.Jump, profile: new ProfileId(1)].Should().Be(expected);
    }

    [Fact]
    public void Indexer_DefaultsToProfileZero()
    {
        var state = new IntentState<TestAction>();
        var expected = new ActionState(true, false, false, Vector2.Zero);
        state.States[(TestAction.Jump, default)] = expected;

        state[TestAction.Jump].Should().Be(expected);
    }
}
