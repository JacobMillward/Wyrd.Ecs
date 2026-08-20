namespace Wyrd.Ecs;

/// <summary>
/// Scheduling anchor: a system declaring <c>[RunAfter(typeof(EndOfUpdatePhase))]</c> (or,
/// more conveniently, <c>[Phase(Phase.PostUpdate)]</c>/<c>.Phase(Phase.PostUpdate)</c>)
/// runs after every other system in its cadence partition, with no edge needed from
/// anything else. See <see cref="StartOfUpdatePhase"/> for the opposite end. Never
/// registered or instantiated, same as any <see cref="MarkerSystem"/>.
/// </summary>
public sealed class EndOfUpdatePhase : MarkerSystem
{
}
