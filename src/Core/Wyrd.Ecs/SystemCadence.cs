namespace Wyrd.Ecs;

/// <summary>
/// How often a system runs relative to <see cref="World.Update"/>. <see cref="Variable"/>
/// (the default, and today's only behavior) runs exactly once per <see cref="World.Update"/>
/// call, at whatever delta the call was given. <see cref="Fixed"/> runs zero or more times
/// per call, at the constant interval <see cref="WorldBuilder.WithFixedTimestep"/> configures
/// (or its default), driven by an accumulator — see <see cref="FixedTimestepAttribute"/>.
/// </summary>
public enum SystemCadence
{
    Variable,
    Fixed,
}
