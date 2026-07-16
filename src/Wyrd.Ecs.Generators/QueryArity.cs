namespace Wyrd.Ecs.Generators;

/// <summary>
/// The single source of truth for how many arities (component type-argument counts)
/// <see cref="QueryTypesGenerator"/> and <see cref="WorldQueryMembersGenerator"/>
/// emit — <c>Query&lt;T0..T{Max-1}&gt;</c>/<c>QueryRow&lt;T0..T{Max-1}&gt;</c> and
/// <c>IWorld</c>/<c>World</c>'s matching <c>Query&lt;T0..T{Max-1}&gt;()</c> overloads,
/// for arity 1 through <see cref="Max"/> inclusive. Raising the cap later is purely
/// additive (see the design's Arity cap section) — this is the one place that needs
/// to change to do it. Not wired to an MSBuild property or feature flag today; a
/// hardcoded constant is the simplest thing that could work, with a single, named,
/// easy-to-find point of control if that changes later.
/// </summary>
internal static class QueryArity
{
    internal const int Max = 8;
}
