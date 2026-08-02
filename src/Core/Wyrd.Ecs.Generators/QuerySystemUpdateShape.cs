using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Wyrd.Ecs.Generators;

/// <summary>
/// Classifies a `QuerySystem` subclass's `Update` method's leading parameters against the
/// canonical order `Time` (mandatory) -&gt; `World` (optional) -&gt; `EntityView` (optional).
/// Shared by <see cref="QueryChainGenerator"/>, <see cref="QueryChainEmitter"/>, and
/// <see cref="Diagnostics.QuerySystemShapeAnalyzer"/>, so the three can never
/// independently drift on what counts as a valid `Update` shape.
/// </summary>
internal static class QuerySystemUpdateShape
{
    internal readonly record struct Result(bool IsValid, bool HasWorld, bool HasEntityView, int ComponentStartIndex);

    internal static readonly Result Invalid = new(false, false, false, 0);

    internal static Result Classify(ImmutableArray<IParameterSymbol> parameters)
    {
        if (parameters.Length == 0 || parameters[0].Type.ToDisplayString() != "Wyrd.Ecs.Time")
            return Invalid;

        var index = 1;

        var hasWorld = index < parameters.Length && parameters[index].Type.ToDisplayString() == "Wyrd.Ecs.World";
        if (hasWorld) index++;

        var hasEntityView = index < parameters.Length && parameters[index].Type.ToDisplayString() == "Wyrd.Ecs.EntityView";
        if (hasEntityView) index++;

        return new Result(true, hasWorld, hasEntityView, index);
    }
}
