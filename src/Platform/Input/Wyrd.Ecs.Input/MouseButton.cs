using SDL3;

namespace Wyrd.Ecs.Input;

/// <summary>A mouse button, kept independent of SDL's raw byte code at the public API surface.</summary>
public enum MouseButton
{
    /// <summary>The left (primary) mouse button.</summary>
    Left,

    /// <summary>The middle mouse button, usually the scroll wheel's click.</summary>
    Middle,

    /// <summary>The right (secondary) mouse button.</summary>
    Right,

    /// <summary>The first extra/side mouse button, if present.</summary>
    X1,

    /// <summary>The second extra/side mouse button, if present.</summary>
    X2,
}

internal static class MouseButtonExtensions
{
    internal static byte ToSdlButton(this MouseButton button) => button switch
    {
        MouseButton.Left => (byte)SDL.ButtonLeft,
        MouseButton.Middle => (byte)SDL.ButtonMiddle,
        MouseButton.Right => (byte)SDL.ButtonRight,
        MouseButton.X1 => (byte)SDL.ButtonX1,
        MouseButton.X2 => (byte)SDL.ButtonX2,
        _ => throw new ArgumentOutOfRangeException(nameof(button)),
    };

    internal static MouseButton? FromSdlButton(byte sdlButton) => sdlButton switch
    {
        SDL.ButtonLeft => MouseButton.Left,
        SDL.ButtonMiddle => MouseButton.Middle,
        SDL.ButtonRight => MouseButton.Right,
        SDL.ButtonX1 => MouseButton.X1,
        SDL.ButtonX2 => MouseButton.X2,
        _ => null,
    };
}
