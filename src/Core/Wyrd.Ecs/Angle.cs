namespace Wyrd.Ecs;

/// <summary>
/// A rotation, stored as radians, normalized to (-pi, pi] on construction. No implicit
/// conversion to or from float, deliberately, matching TimeSpan's discipline: every value
/// has to go through Deg or Rad, so a caller can never hand a bare radians or degrees
/// value where the other unit was expected.
/// </summary>
public readonly record struct Angle
{
    /// <summary>This angle's value in radians, normalized to (-pi, pi].</summary>
    public float Radians { get; private init => field = Normalize(value); }

    /// <summary>Builds an <see cref="Angle"/> from a value in degrees.</summary>
    public static Angle Deg(float degrees) => new() { Radians = degrees * (MathF.PI / 180f) };

    /// <summary>Builds an <see cref="Angle"/> from a value in radians.</summary>
    public static Angle Rad(float radians) => new() { Radians = radians };

    /// <summary>This angle's value in degrees, normalized to (-180, 180].</summary>
    public float Degrees => Radians * (180f / MathF.PI);

    /// <summary>No rotation.</summary>
    public static readonly Angle Zero = new() { Radians = 0f };

    /// <summary>Sums two angles.</summary>
    public static Angle operator +(Angle a, Angle b) => new() { Radians = a.Radians + b.Radians };

    /// <summary>Subtracts <paramref name="b"/> from <paramref name="a"/>.</summary>
    public static Angle operator -(Angle a, Angle b) => new() { Radians = a.Radians - b.Radians };

    /// <summary>Scales <paramref name="a"/> by <paramref name="scalar"/>.</summary>
    public static Angle operator *(Angle a, float scalar) => new() { Radians = a.Radians * scalar };

    // Ceiling, not floor: puts the closed boundary at +pi (Rad(MathF.PI).Degrees == 180,
    // not -180), matching the (-pi, pi] range documented on Radians/Degrees above.
    private static float Normalize(float radians) =>
        radians - MathF.Tau * MathF.Ceiling((radians - MathF.PI) / MathF.Tau);
}
