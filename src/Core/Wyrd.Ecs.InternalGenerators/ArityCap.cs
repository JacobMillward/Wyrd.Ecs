namespace Wyrd.Ecs.InternalGenerators;

/// <summary>
/// The arity cap for <see cref="WorldQueryMembersGenerator"/>'s multi-component
/// <c>CommandBuffer.CreateEntity&lt;T0..T{Max-1}&gt;(...)</c> overloads, arity 1
/// through <see cref="Max"/> inclusive. Not wired to an MSBuild property or feature
/// flag — a hardcoded constant is the simplest thing that could work, with a single,
/// named, easy-to-find point of control if that changes later. A separate, duplicated
/// `ArityCap` exists in `Wyrd.Ecs.Generators` for its own `WithSystems&lt;T0..Tn&gt;()`
/// overloads (Task 7) — the two are deliberately independent constants in independent
/// generator projects, not shared via a project reference.
/// </summary>
internal static class ArityCap
{
    internal const int Max = 8;
}
