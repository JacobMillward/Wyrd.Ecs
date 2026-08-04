namespace Wyrd.Ecs;

/// <summary>
/// Declares that the annotated system runs at <see cref="SystemCadence.Fixed"/> cadence
/// instead of the default <see cref="SystemCadence.Variable"/>: stepped zero or more times
/// per <see cref="World.Update"/> call by the fixed-step accumulator, rather than exactly
/// once at whatever delta the call was given. Not stackable (a system has exactly one
/// cadence) and not inherited (a subclass restates its own cadence, same rule as
/// <see cref="RunBeforeAttribute"/>/<see cref="RunAfterAttribute"/>). Read at compile time
/// by the query-chain generator into <c>Wyrd.Ecs.Generated.SystemRegistry.Cadence</c>, not
/// via runtime reflection.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FixedTimestepAttribute : Attribute
{
}
