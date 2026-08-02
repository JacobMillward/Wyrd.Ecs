namespace Wyrd.Ecs.Generators;

/// <summary>
/// Arity cap for the generated <c>WithSystems&lt;T0..T{Max-1}&gt;()</c> overloads (see
/// <see cref="QueryChainEmitter.RenderWithSystemsExtensions"/>). Kept separate from
/// <c>Wyrd.Ecs.InternalGenerators.ArityCap</c> since the two generator projects target
/// independent compilations and aren't linked by a project reference.
/// </summary>
internal static class ArityCap
{
    internal const int Max = 8;
}
