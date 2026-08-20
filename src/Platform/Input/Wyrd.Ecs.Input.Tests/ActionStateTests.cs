using System.Numerics;

namespace Wyrd.Ecs.Input.Tests;

public class ActionStateTests
{
    [Fact]
    public void TwoActionStatesWithTheSameValues_AreEqual()
    {
        var a = new ActionState(IsHeld: true, JustPressed: true, JustReleased: false, Value: Vector2.UnitX);
        var b = new ActionState(IsHeld: true, JustPressed: true, JustReleased: false, Value: Vector2.UnitX);

        a.Should().Be(b);
    }

    [Theory]
    [InlineData(MouseButton.Left, 1)]
    [InlineData(MouseButton.Middle, 2)]
    [InlineData(MouseButton.Right, 3)]
    [InlineData(MouseButton.X1, 4)]
    [InlineData(MouseButton.X2, 5)]
    public void MouseButtonRoundTripsThroughTheSdlRawByte(MouseButton button, byte expectedSdlValue)
    {
        var sdlValue = MouseButtonExtensions.ToSdlButton(button);

        sdlValue.Should().Be(expectedSdlValue);
        MouseButtonExtensions.FromSdlButton(sdlValue).Should().Be(button);
    }

    [Fact]
    public void UnrecognizedSdlByte_FromSdlButtonReturnsNull() =>
        MouseButtonExtensions.FromSdlButton(200).Should().BeNull();
}
