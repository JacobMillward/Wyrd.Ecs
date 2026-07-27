using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Generators;

internal static class ChainWalker
{
    internal static QueryShape? TryExtractShape(InvocationExpressionSyntax terminal, SemanticModel semanticModel, CancellationToken ct)
    {
        if (terminal.Expression is not MemberAccessExpressionSyntax { Expression: var receiverExpr }) return null;
        if (semanticModel.GetTypeInfo(receiverExpr, ct).Type is not INamedTypeSymbol receiverType) return null;

        var raw = TryExtractShapeFromQueryType(receiverType, ct);
        if (raw is null) return null;
        if (raw.PendingDataElements.IsEmpty) return raw; // filter-only shape, e.g. .Has<T>() alone

        if (terminal.ArgumentList.Arguments is not [.., var lastArgument]) return null;
        // Every argument before the lambda is a leading uniform/state value the lambda
        // receives as its own leading parameter(s) -- the uniform overload passes one
        // (`ForEach(state, action)`, lambda takes `(in TState, ...)`), the no-uniform
        // overload passes none (`ForEach(action)`, lambda's first parameter is already a
        // real data component). Skip exactly that many of the lambda's own parameters
        // before treating the rest as data.
        var skipCount = terminal.ArgumentList.Arguments.Count - 1;
        var refKinds = TryGetLambdaDataRefKinds(lastArgument, skipCount);
        if (refKinds is null) return null;

        return ResolveAccessKinds(raw, refKinds.Value);
    }

    /// <summary>
    /// Reads the <c>ref</c>/<c>in</c> modifier off each of <paramref name="lambdaArgument"/>'s
    /// data parameters, in declaration order, skipping <paramref name="skipCount"/> leading
    /// uniform/state parameters. Pure syntax -- no semantic binding needed, since the
    /// modifier keyword is right there in the parameter list regardless of whether the
    /// lambda has explicit parameter types.
    /// </summary>
    private static ImmutableArray<RefKind>? TryGetLambdaDataRefKinds(ArgumentSyntax lambdaArgument, int skipCount)
    {
        if (lambdaArgument.Expression is not ParenthesizedLambdaExpressionSyntax lambda) return null;
        if (lambda.ParameterList.Parameters.Count < skipCount) return null;

        var dataParameters = lambda.ParameterList.Parameters.Skip(skipCount);
        var builder = ImmutableArray.CreateBuilder<RefKind>();
        foreach (var parameter in dataParameters)
        {
            builder.Add(parameter.Modifiers switch
            {
                var m when m.Any(SyntaxKind.RefKeyword) => RefKind.Ref,
                var m when m.Any(SyntaxKind.InKeyword) => RefKind.In,
                _ => RefKind.None,
            });
        }
        return builder.ToImmutable();
    }

    /// <summary>
    /// Resolves <paramref name="raw"/>'s <see cref="QueryShape.PendingDataElements"/> (bare
    /// data components whose Reads/Writes kind wasn't yet known during the tuple walk) into
    /// real <see cref="MarkerElement"/>s, using <paramref name="refKindsInDeclarationOrder"/>
    /// — the ref/in modifiers read off whatever the query's terminal actually is, in the same
    /// left-to-right order the caller wrote their `.With&lt;&gt;()` calls (matching the
    /// existing declaration-order convention <c>QueryShapeExtensions.OwnDataElements</c>
    /// already relies on). Returns <c>null</c> if the counts don't match (caller declared a
    /// different number of components than the terminal has parameters for) or any ref-kind
    /// isn't <see cref="RefKind.Ref"/>/<see cref="RefKind.In"/> (a bare, unmodified parameter
    /// -- <c>WYRD001</c>'s job to explain why, not this method's).
    /// </summary>
    internal static QueryShape? ResolveAccessKinds(QueryShape raw, ImmutableArray<RefKind> refKindsInDeclarationOrder)
    {
        // PendingDataElements is outer-first (reverse-declaration) order, same as Markers --
        // reverse it back to left-to-right, same convention OwnDataElements already uses.
        var declarationOrder = raw.PendingDataElements.Reverse().ToImmutableArray();
        if (declarationOrder.Length != refKindsInDeclarationOrder.Length) return null;

        var resolvedInDeclarationOrder = ImmutableArray.CreateBuilder<MarkerElement>(declarationOrder.Length);
        for (var i = 0; i < declarationOrder.Length; i++)
        {
            var kind = refKindsInDeclarationOrder[i] switch
            {
                RefKind.Ref => MarkerKind.Writes,
                RefKind.In => MarkerKind.Reads,
                _ => (MarkerKind?)null,
            };
            if (kind is null) return null;
            resolvedInDeclarationOrder.Add(new MarkerElement(kind.Value, declarationOrder[i]));
        }

        // Markers is stored outer-first (reverse-declaration) order everywhere else in
        // this file -- OwnDataElements() unconditionally reverses it once to recover
        // declaration order. Append these newly-resolved markers in that same outer-first
        // order (i.e. reversed from the declaration order just used for the ref-kind zip
        // above), or OwnDataElements()'s single reversal flips them the wrong way.
        var resolved = raw.Markers.ToBuilder();
        for (var i = resolvedInDeclarationOrder.Count - 1; i >= 0; i--)
            resolved.Add(resolvedInDeclarationOrder[i]);

        return new QueryShape
        {
            ExactShapeTypeName = raw.ExactShapeTypeName,
            Markers = resolved.ToImmutable(),
            PendingDataElements = ImmutableArray<string>.Empty,
            Withouts = raw.Withouts,
            Anys = raw.Anys,
        };
    }

    /// <summary>
    /// Unpacks an already-resolved <c>Wyrd.Ecs.Query&lt;TShape&gt;</c> type symbol's
    /// nested-tuple <c>TShape</c> directly — shared by <see cref="TryExtractShape"/>
    /// (a chain terminal's receiver expression type) and <c>QuerySystem</c> recognition
    /// (a <c>Build</c> method's declared return type, Task 11) — both start from a
    /// resolved <c>Query&lt;TShape&gt;</c> symbol, they just get it different ways.
    /// </summary>
    internal static QueryShape? TryExtractShapeFromQueryType(INamedTypeSymbol queryType, CancellationToken ct)
    {
        if (!IsQueryOfShape(queryType)) return null;
        if (queryType.TypeArguments is not [var shapeType]) return null;

        var markers = ImmutableArray.CreateBuilder<MarkerElement>();
        var pendingData = ImmutableArray.CreateBuilder<string>();
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

            if (!TryClassifyElement(element, markers, pendingData, withouts, anys)) return null;

            current = rest;
        }

        return new QueryShape
        {
            ExactShapeTypeName = queryType.ToDisplayString(),
            Markers = markers.ToImmutable(),
            PendingDataElements = pendingData.ToImmutable(),
            Withouts = withouts.ToImmutable(),
            Anys = anys.ToImmutable(),
        };
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

    private static bool TryClassifyElement(
        ITypeSymbol element,
        ImmutableArray<MarkerElement>.Builder markers,
        ImmutableArray<string>.Builder pendingData,
        ImmutableArray<WithoutElement>.Builder withouts,
        ImmutableArray<AnyElement>.Builder anys)
    {
        if (element is not INamedTypeSymbol named) return false;
        var original = named.OriginalDefinition;

        // A bare struct with no Wyrd.Ecs wrapper -- e.g. .With<Position>() -- is a
        // pending data element; its Reads/Writes kind isn't known until the terminal
        // (a .ForEach lambda's ref/in, or QuerySystem.Update's real parameters) is
        // read, which happens after this whole tuple walk finishes. See
        // ChainWalker.ResolveAccessKinds.
        if (original.ContainingNamespace?.ToDisplayString() != "Wyrd.Ecs")
        {
            pendingData.Add(element.ToDisplayString());
            return true;
        }

        switch (original.Name)
        {
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
                pendingData.Add(element.ToDisplayString());
                return true;
        }
    }

    internal static bool IsQueryOfShape(INamedTypeSymbol type)
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
