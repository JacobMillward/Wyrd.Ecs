using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.SystemGenerators;

internal static class ChainWalker
{
    internal static QueryShape? TryExtractShape(InvocationExpressionSyntax terminal, SemanticModel semanticModel, CancellationToken ct)
    {
        if (terminal.Expression is not MemberAccessExpressionSyntax { Expression: var receiverExpr }) return null;

        if (semanticModel.GetTypeInfo(receiverExpr, ct).Type is not INamedTypeSymbol receiverType) return null;
        if (!IsQueryOfShape(receiverType)) return null;
        if (receiverType.TypeArguments is not [var shapeType]) return null;

        var markers = ImmutableArray.CreateBuilder<MarkerElement>();
        var withouts = ImmutableArray.CreateBuilder<WithoutElement>();
        var anys = ImmutableArray.CreateBuilder<AnyElement>();

        var current = shapeType;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (current is not INamedTypeSymbol named) return null;
            if (IsNil(named)) break;

            if (!named.IsTupleType || named.TupleElements.Length != 2) return null;
            var element = named.TupleElements[0].Type;
            var rest = named.TupleElements[1].Type;

            if (!TryClassifyElement(element, markers, withouts, anys)) return null;

            current = rest;
        }

        return new QueryShape
        {
            ExactShapeTypeName = receiverType.ToDisplayString(),
            Markers = markers.ToImmutable(),
            Withouts = withouts.ToImmutable(),
            Anys = anys.ToImmutable(),
        };
    }

    private static bool TryClassifyElement(
        ITypeSymbol element,
        ImmutableArray<MarkerElement>.Builder markers,
        ImmutableArray<WithoutElement>.Builder withouts,
        ImmutableArray<AnyElement>.Builder anys)
    {
        if (element is not INamedTypeSymbol named) return false;
        var original = named.OriginalDefinition;
        if (original.ContainingNamespace?.ToDisplayString() != "Wyrd.Ecs") return false;

        switch (original.Name)
        {
            case "Writes" when named.TypeArguments is [var t]:
                markers.Add(new MarkerElement(MarkerKind.Writes, t.ToDisplayString()));
                return true;
            case "Reads" when named.TypeArguments is [var t]:
                markers.Add(new MarkerElement(MarkerKind.Reads, t.ToDisplayString()));
                return true;
            case "Has" when named.TypeArguments is [var t]:
                markers.Add(new MarkerElement(MarkerKind.Has, t.ToDisplayString()));
                return true;
            case "Without" when named.TypeArguments is [var t]:
                withouts.Add(new WithoutElement(t.ToDisplayString()));
                return true;
            case "Any" when named.TypeArguments is [var t0, var t1]:
                anys.Add(new AnyElement(t0.ToDisplayString(), t1.ToDisplayString()));
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// The fully qualified name of the <see cref="Wyrd.Ecs.EcsSystem"/> subclass whose
    /// <c>OnUpdate</c> override directly contains <paramref name="terminal"/>, or
    /// <c>null</c> if it isn't inside one — walks the override chain, not just the
    /// method name, so a same-named method that isn't actually an <c>EcsSystem</c>
    /// override never matches.
    /// </summary>
    internal static string? TryFindEnclosingSystemType(InvocationExpressionSyntax terminal, SemanticModel semanticModel, CancellationToken ct)
    {
        var methodDecl = terminal.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDecl is null) return null;
        if (semanticModel.GetDeclaredSymbol(methodDecl, ct) is not IMethodSymbol method) return null;
        if (method.Name != "OnUpdate" || !method.IsOverride) return null;

        for (var overridden = method.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
        {
            ct.ThrowIfCancellationRequested();
            if (overridden.ContainingType.Name == "EcsSystem" && overridden.ContainingType.ContainingNamespace?.ToDisplayString() == "Wyrd.Ecs")
                return method.ContainingType.ToDisplayString();
        }

        return null;
    }

    private static bool IsQueryOfShape(INamedTypeSymbol type)
    {
        var original = type.OriginalDefinition;
        return original.Name == "Query" && original.Arity == 1 && original.ContainingNamespace?.ToDisplayString() == "Wyrd.Ecs";
    }

    private static bool IsNil(INamedTypeSymbol type)
    {
        var original = type.OriginalDefinition;
        return original.Name == "Nil" && original.ContainingNamespace?.ToDisplayString() == "Wyrd.Ecs";
    }
}
