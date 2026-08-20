namespace Wyrd.Ecs;

/// <summary>
/// Scheduling anchor: a system declaring <c>[RunBefore(typeof(StartOfUpdatePhase))]</c>
/// (or, more conveniently, <c>[Phase(Phase.PreUpdate)]</c>/<c>.Phase(Phase.PreUpdate)</c>)
/// runs before every other system in its cadence partition, with no edge needed from
/// anything else. See <see cref="EndOfUpdatePhase"/> for the opposite end. Never
/// registered or instantiated, same as any <see cref="MarkerSystem"/>.
/// </summary>
public sealed class StartOfUpdatePhase : MarkerSystem
{
}
