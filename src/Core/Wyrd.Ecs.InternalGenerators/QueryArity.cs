namespace Wyrd.Ecs.InternalGenerators;

/// <summary>
/// The arity cap for <see cref="WorldQueryMembersGenerator"/>'s multi-component
/// <c>CommandBuffer.CreateEntity&lt;T0..T{Max-1}&gt;(...)</c> overloads, arity 1
/// through <see cref="Max"/> inclusive. Previously also governed
/// <c>Query&lt;T0..T{Max-1}&gt;</c>/<c>QueryRow&lt;T0..T{Max-1}&gt;</c>; that family
/// was removed when queries moved to the generator-backed unbounded query-shape
/// design, which has no arity cap. Not wired to an MSBuild property or feature flag —
/// a hardcoded constant is the simplest thing that could work, with a single, named,
/// easy-to-find point of control if that changes later.
/// </summary>
internal static class QueryArity
{
    internal const int Max = 8;
}
