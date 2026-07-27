namespace Wyrd.Ecs.Generators;

/// <summary>
/// The arity cap for the generated <c>WithSystems&lt;T0..T{Max-1}&gt;()</c> overloads
/// (see <see cref="QueryChainEmitter.RenderWithSystemsExtensions"/>). Deliberately its
/// own, separate constant from `Wyrd.Ecs.InternalGenerators.ArityCap` — same value
/// today, independent generator projects targeting independent compilations, not
/// shared via a project reference (one line to keep in sync if it ever needs to
/// change, matching the "simplest thing that could work" precedent the original
/// `QueryArity` was built on).
/// </summary>
internal static class ArityCap
{
    internal const int Max = 8;
}
