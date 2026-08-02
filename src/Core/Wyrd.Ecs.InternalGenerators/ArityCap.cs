namespace Wyrd.Ecs.InternalGenerators;

/// <summary>
/// Arity cap (1 through <see cref="Max"/>) for <see cref="WorldQueryMembersGenerator"/>'s
/// multi-component <c>CommandBuffer.CreateEntity&lt;T0..T{Max-1}&gt;(...)</c> overloads.
/// Hardcoded for a single, easy-to-find point of control. A separate, independent
/// <c>ArityCap</c> in <c>Wyrd.Ecs.Generators</c> serves that project's own
/// <c>WithSystems&lt;T0..Tn&gt;()</c> overloads; the two are not shared via project reference.
/// </summary>
internal static class ArityCap
{
    internal const int Max = 8;
}
