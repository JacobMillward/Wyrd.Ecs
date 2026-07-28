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
        // raw.PendingDataElements is already in declaration order (QueryShape.Markers and
        // QueryShape.PendingDataElements are both normalized to declaration order once, in
        // TryExtractShapeFromQueryType) -- no reversal needed to line it up with
        // refKindsInDeclarationOrder.
        var declarationOrder = raw.PendingDataElements;
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

        // raw.Markers is already in declaration order too, so the newly-resolved markers
        // append directly, in the same order just computed above.
        var resolved = raw.Markers.ToBuilder();
        resolved.AddRange(resolvedInDeclarationOrder);

        return new QueryShape
        {
            ExactShapeTypeName = raw.ExactShapeTypeName,
            Markers = resolved.ToImmutable(),
            PendingDataElements = ImmutableArray<string>.Empty,
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

        var pendingData = ImmutableArray.CreateBuilder<string>();

        var current = shapeType;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (current is not INamedTypeSymbol named) return null;
            if (IsNil(named)) break;

            if (!named.IsTupleType || named.TupleElements.Length != 2) return null;
            var element = named.TupleElements[0].Type;
            var rest = named.TupleElements[1].Type;

            if (!TryClassifyElement(element, pendingData)) return null;

            current = rest;
        }

        // The walk above visits `.With<A>().With<B>()`'s nested-tuple type `(B, (A, Nil))`
        // outer-first, i.e. last-declared-first -- the reverse of declaration order. Reverse
        // once, here, so QueryShape.PendingDataElements is in declaration order everywhere
        // downstream (OwnDataElements, ResolveAccessKinds, every caller-facing parameter list).
        pendingData.Reverse();

        return new QueryShape
        {
            ExactShapeTypeName = queryType.ToDisplayString(),
            Markers = ImmutableArray<MarkerElement>.Empty,
            PendingDataElements = pendingData.ToImmutable(),
        };
    }

    /// <summary>
    /// The fully qualified name of the <see cref="Wyrd.Ecs.EcsSystem"/> subclass whose
    /// <c>Execute</c> override directly contains <paramref name="terminal"/>, or
    /// <c>null</c> if it isn't inside one — walks the override chain, not just the
    /// method name, so a same-named method that isn't actually an <c>EcsSystem</c>
    /// override never matches.
    /// </summary>
    internal static string? TryFindEnclosingSystemType(InvocationExpressionSyntax terminal, SemanticModel semanticModel, CancellationToken ct)
    {
        var methodDecl = terminal.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDecl is null) return null;
        if (semanticModel.GetDeclaredSymbol(methodDecl, ct) is not IMethodSymbol method) return null;
        if (method.Name != "Execute" || !method.IsOverride) return null;

        for (var overridden = method.OverriddenMethod; overridden is not null; overridden = overridden.OverriddenMethod)
        {
            ct.ThrowIfCancellationRequested();
            if (overridden.ContainingType.Name == "EcsSystem" && overridden.ContainingType.ContainingNamespace?.ToDisplayString() == "Wyrd.Ecs")
                return method.ContainingType.ToDisplayString();
        }

        return null;
    }

    private static bool TryClassifyElement(ITypeSymbol element, ImmutableArray<string>.Builder pendingData)
    {
        if (element is not INamedTypeSymbol) return false;

        // Every tuple element left after the runtime-filter-unification redesign is a bare
        // data component -- .Without/.Has/.Any never touch TShape anymore (see Query.cs),
        // so there is nothing else for this walk to classify. Its Reads/Writes kind isn't
        // known until the terminal (a .ForEach lambda's ref/in, or QuerySystem.Update's real
        // parameters) is read, which happens after this whole tuple walk finishes. See
        // ChainWalker.ResolveAccessKinds.
        pendingData.Add(element.ToDisplayString());
        return true;
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
