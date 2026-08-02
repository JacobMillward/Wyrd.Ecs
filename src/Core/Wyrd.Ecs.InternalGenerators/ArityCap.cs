namespace Wyrd.Ecs.InternalGenerators;

/// <summary>
/// Arity cap (1 through <see cref="Max"/>) for <see cref="WorldQueryMembersGenerator"/>'s
/// multi-component <c>CommandBuffer.CreateEntity&lt;T0..T{Max-1}&gt;(...)</c> overloads.
/// Hardcoded for a single, easy-to-find point of control.
/// </summary>
internal static class ArityCap
{
    internal const int Max = 8;
}
