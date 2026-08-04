namespace Wyrd.Ecs;

/// <summary>
/// How often a system runs relative to <see cref="World.Update"/>. Set via
/// <see cref="FixedTimestepAttribute"/>; the interval itself is configured by
/// <see cref="WorldBuilder.WithFixedTimestep"/>.
/// </summary>
public enum SystemCadence
{
    /// <summary>Runs exactly once per <see cref="World.Update"/> call, at whatever delta the call was given. The default.</summary>
    Variable,

    /// <summary>Runs zero or more times per <see cref="World.Update"/> call, at a constant interval driven by an accumulator.</summary>
    Fixed,
}
