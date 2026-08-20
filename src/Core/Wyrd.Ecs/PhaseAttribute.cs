namespace Wyrd.Ecs;

/// <summary>
/// Declares which <see cref="Wyrd.Ecs.Phase"/> the annotated system runs in. Sugar for
/// <c>[RunBefore(typeof(StartOfUpdatePhase))]</c>/<c>[RunAfter(typeof(EndOfUpdatePhase))]</c>
/// - the query-chain generator translates it into the identical
/// <c>Wyrd.Ecs.Generated.SystemRegistry.Edges</c> entry those attributes would produce by
/// hand, so it composes with explicit <see cref="RunBeforeAttribute"/>/
/// <see cref="RunAfterAttribute"/> edges exactly like any other pair of edges would - a
/// genuinely contradictory combination surfaces as a named cycle at schedule-build time
/// (<see cref="Internal.StableTopologicalSort"/>), not silently. Not stackable (a system
/// has exactly one phase) and not inherited, same rule as <see cref="FixedTimestepAttribute"/>.
/// A generic <c>EcsSystem</c> can never carry this attribute (or <see cref="RunBeforeAttribute"/>/
/// <see cref="RunAfterAttribute"/>) - the generator can't emit a registry entry for a
/// class's own open type parameter; use the fluent <see cref="SystemRegistration.Phase"/>
/// at the registration call site instead.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PhaseAttribute(Phase phase) : Attribute
{
    /// <summary>The phase this system runs in.</summary>
    public Phase Phase { get; } = phase;
}
