using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Generators;

internal static class ChainWalker
{
    internal static QueryShape? TryExtractShape(InvocationExpressionSyntax terminal, SemanticModel semanticModel, CancellationToken ct, out bool includesEntityView)
    {
        includesEntityView = false;

        if (terminal.Expression is not MemberAccessExpressionSyntax { Expression: var receiverExpr }) return null;
        if (semanticModel.GetTypeInfo(receiverExpr, ct).Type is not INamedTypeSymbol receiverType) return null;

        var raw = TryExtractShapeFromQueryType(receiverType, ct);
        if (raw is null) return null;

        // Every argument before the lambda is a leading uniform/state value the lambda
        // receives as its own leading parameter(s): the uniform overload passes one
        // (`ForEach(state, action)`, lambda takes `(in TState, ...)`), the no-uniform
        // overload passes none (`ForEach(action)`, lambda's first parameter is already a
        // real data component). Skip exactly that many of the lambda's own parameters
        // before treating the rest as data -- and, ahead of that, one more if it's a
        // recognized leading EntityView parameter.
        if (terminal.ArgumentList.Arguments is not [.., var lastArgument])
            return raw.PendingDataElements.IsEmpty ? raw : null;

        var skipCount = terminal.ArgumentList.Arguments.Count - 1;
        var lambda = lastArgument.Expression as ParenthesizedLambdaExpressionSyntax;

        if (raw.PendingDataElements.IsEmpty)
        {
            if (lambda is not null) includesEntityView = IsEntityViewParameterAt(lambda, skipCount, semanticModel, ct);
            return raw; // filter-only shape, e.g. .Has<T>() alone -- optionally with a solo EntityView parameter
        }

        if (lambda is null) return null;
        includesEntityView = IsEntityViewParameterAt(lambda, skipCount, semanticModel, ct);

        var refKinds = TryGetLambdaDataRefKinds(lastArgument, skipCount + (includesEntityView ? 1 : 0));
        if (refKinds is null) return null;

        return ResolveAccessKinds(raw, refKinds.Value);
    }

    /// <summary>True if <paramref name="lambda"/> has a parameter at <paramref name="index"/> and it's a recognized <see cref="IsEntityViewParameter"/>.</summary>
    private static bool IsEntityViewParameterAt(ParenthesizedLambdaExpressionSyntax lambda, int index, SemanticModel semanticModel, CancellationToken ct) =>
        index < lambda.ParameterList.Parameters.Count && IsEntityViewParameter(lambda.ParameterList.Parameters[index], semanticModel, ct);

    /// <summary>
    /// True if <paramref name="parameter"/> is a plain (no `ref`/`in` modifier), explicitly-typed
    /// `Wyrd.Ecs.EntityView` parameter -- the fluent chain's equivalent of
    /// `QuerySystemUpdateShape.Classify`'s `EntityView` recognition. Shared by
    /// <see cref="TryExtractShape"/>'s entity-parameter detection and
    /// `Diagnostics.BareDataParameterAnalyzer`'s WYRD001 check, so the two can never
    /// independently drift on what counts as a recognized leading `EntityView` parameter.
    /// Resolved through the semantic model, not string-matched: an explicit parameter type's
    /// semantic type is knowable independent of which overload the invocation itself resolves
    /// to, unlike a lambda body's return type (see `QueryChainGenerator.ClassifyTerminalKind`).
    /// </summary>
    internal static bool IsEntityViewParameter(ParameterSyntax parameter, SemanticModel semanticModel, CancellationToken ct)
    {
        if (parameter.Modifiers.Any(SyntaxKind.RefKeyword) || parameter.Modifiers.Any(SyntaxKind.InKeyword)) return false;
        if (parameter.Type is not { } typeSyntax) return false;
        var type = semanticModel.GetTypeInfo(typeSyntax, ct).Type;
        return type is { Name: "EntityView", ContainingNamespace.Name: "Ecs" } && type.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs";
    }

    /// <summary>
    /// Reads the <c>ref</c>/<c>in</c> modifier off each of <paramref name="lambdaArgument"/>'s
    /// data parameters, in declaration order, skipping <paramref name="skipCount"/> leading
    /// uniform/state parameters. Pure syntax: no semantic binding needed, since the modifier
    /// keyword is right there in the parameter list regardless of whether the lambda has
    /// explicit parameter types.
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
    /// Resolves <paramref name="raw"/>'s <see cref="QueryShape.PendingDataElements"/> into
    /// real <see cref="MarkerElement"/>s using <paramref name="refKindsInDeclarationOrder"/>,
    /// the ref/in modifiers read off the query's terminal in the same order the caller
    /// wrote their `.With&lt;&gt;()` calls. Returns <c>null</c> if the counts don't match
    /// or any ref-kind isn't <see cref="RefKind.Ref"/>/<see cref="RefKind.In"/> (reporting
    /// that is <c>WYRD001</c>'s job, not this method's).
    /// </summary>
    internal static QueryShape? ResolveAccessKinds(QueryShape raw, ImmutableArray<RefKind> refKindsInDeclarationOrder)
    {
        // raw.PendingDataElements is already in declaration order (normalized once, in
        // TryExtractShapeFromQueryType), so no reversal is needed to line it up with
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
    /// nested-tuple <c>TShape</c>. Shared by <see cref="TryExtractShape"/> (a chain
    /// terminal's receiver expression type) and <c>QuerySystem</c> recognition (a
    /// <c>Build</c> method's declared return type): both start from a resolved
    /// <c>Query&lt;TShape&gt;</c> symbol, just obtained differently.
    /// </summary>
    internal static QueryShape? TryExtractShapeFromQueryType(INamedTypeSymbol queryType, CancellationToken ct)
    {
        if (!IsQueryOfShape(queryType)) return null;

        var pendingData = ImmutableArray.CreateBuilder<string>();

        // The non-generic `Query` (arity 0) is the chain's entry point, already the empty
        // shape: no `TShape` to walk. Only `Query<TShape>` (arity 1) has a tuple to unpack.
        if (queryType.Arity == 1)
        {
            if (queryType.TypeArguments is not [var shapeType]) return null;

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
        }

        // The walk above visits `.With<A>().With<B>()`'s nested-tuple type `(B, (A, Nil))`
        // outer-first, i.e. last-declared-first: the reverse of declaration order. Reverse
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
    /// Walks <paramref name="queryType"/>'s nested tuple shape to find a `file`-scoped
    /// component type, if any: these can never work, since the generator's emitted
    /// terminals/glue live in a separate generated source file that cannot reference a
    /// `file`-scoped type from the consumer's own file (see WYRD004). Returns the first
    /// offending type's simple name, or <c>null</c> if none.
    /// </summary>
    internal static string? TryFindFileLocalComponentType(INamedTypeSymbol queryType, CancellationToken ct)
    {
        if (!IsQueryOfShape(queryType)) return null;
        if (queryType.Arity == 0) return null; // the entry point: no TShape tuple to walk yet

        if (queryType.TypeArguments is not [var shapeType]) return null;

        var current = shapeType;
        while (current is INamedTypeSymbol named && !IsNil(named))
        {
            ct.ThrowIfCancellationRequested();

            if (!named.IsTupleType || named.TupleElements.Length != 2) return null;
            if (named.TupleElements[0].Type is INamedTypeSymbol { IsFileLocal: true } fileLocal)
                return fileLocal.Name;

            current = named.TupleElements[1].Type;
        }

        return null;
    }

    /// <summary>
    /// The fully qualified name of the <see cref="Wyrd.Ecs.EcsSystem"/> subclass containing
    /// <paramref name="terminal"/>, or <c>null</c> if it isn't inside one. Any method of the
    /// class counts, not only a literal <c>Execute</c> override directly containing the call
    /// -- a call factored into a private helper method that <c>Execute</c> calls is still
    /// attributed to the system.
    /// </summary>
    internal static string? TryFindEnclosingSystemType(InvocationExpressionSyntax terminal, SemanticModel semanticModel, CancellationToken ct)
    {
        var containingClass = terminal.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (containingClass is null) return null;
        if (semanticModel.GetDeclaredSymbol(containingClass, ct) is not INamedTypeSymbol classSymbol) return null;

        for (var current = classSymbol.BaseType; current is not null; current = current.BaseType)
        {
            ct.ThrowIfCancellationRequested();
            if (current.Name == "EcsSystem" && current.ContainingNamespace?.ToDisplayString() == "Wyrd.Ecs")
                return classSymbol.ToDisplayString();
        }

        return null;
    }

    private static bool TryClassifyElement(ITypeSymbol element, ImmutableArray<string>.Builder pendingData)
    {
        if (element is not INamedTypeSymbol) return false;

        // Every tuple element is a bare data component: .Without/.Has/.Any never touch
        // TShape. Its Reads/Writes kind isn't known until the terminal is read, after this
        // whole tuple walk finishes. See ChainWalker.ResolveAccessKinds.
        pendingData.Add(element.ToDisplayString());
        return true;
    }

    internal static bool IsQueryOfShape(INamedTypeSymbol type)
    {
        var original = type.OriginalDefinition;
        return original.Name == "Query" && original.Arity is 0 or 1 && original.ContainingNamespace?.ToDisplayString() == "Wyrd.Ecs";
    }

    /// <summary>
    /// True if <paramref name="type"/> derives (directly or transitively) from
    /// <c>Wyrd.Ecs.EcsSystem</c>. Shared by the generator's own candidate extraction (e.g.
    /// constructor-shape/resource-glue discovery) and the diagnostics analyzers that need the
    /// same check (e.g. <c>UnusedResourceWriteAnalyzer</c>) -- one walk, not one per caller.
    /// </summary>
    internal static bool InheritsFromEcsSystem(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (current is { Name: "EcsSystem", ContainingNamespace.Name: "Ecs" } && current.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs")
                return true;
        return false;
    }

    /// <summary>The invoked method's simple name, for an invocation shaped `receiver.Name(...)`, or <c>null</c> for any other call shape.</summary>
    internal static string? TryGetInvokedMethodName(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax { Name: IdentifierNameSyntax { Identifier.ValueText: var name } } ? name : null;

    /// <summary>The chain-terminal method names this generator recognizes. Centralized so the syntax-provider predicate, terminal-kind classification, and the CS9198 suppressor's own call-site check can't drift apart.</summary>
    internal static bool IsChainTerminalMethodName(string name) => name is "ForEach" or "ParallelForEach";

    private static bool IsNil(INamedTypeSymbol type)
    {
        var original = type.OriginalDefinition;
        return original.Name == "Nil" && original.ContainingNamespace?.ToDisplayString() == "Wyrd.Ecs";
    }
}
