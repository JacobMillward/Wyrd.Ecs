using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Wyrd.Ecs.Generators.Diagnostics;

namespace Wyrd.Ecs.Generators;

/// <summary>
/// Finds every `.ForEach`/`.ParallelForEach` terminal call on a fluent query chain, and
/// every `QuerySystem` subclass's `DefineQuery` override, anywhere in the consuming
/// project's source. Extracts each one's shape (<see cref="ChainWalker"/>) and emits
/// terminal methods, `QuerySystem` glue, and a `GeneratedSystemAccess` registry entry.
/// Groups chain terminals two levels deep: exact declaration-order tuple type (one
/// overload each) nested inside logical shape (one shared backend each).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class QueryChainGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
            ctx.AddSource("WithSystemsExtensions.g.cs", QueryChainEmitter.RenderWithSystemsExtensions()));

        var chainCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax
                    {
                        Name: IdentifierNameSyntax { Identifier.ValueText: "ForEach" or "ParallelForEach" }
                    }
                },
                transform: static (ctx, ct) => ExtractChainCandidate((InvocationExpressionSyntax)ctx.Node, ctx.SemanticModel, ct))
            .Where(static c => c.Shape is not null || c.Diagnostic is not null)
            .WithTrackingName("QueryChainShape");

        var querySystemCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList.Types.Count: > 0 },
                transform: static (ctx, ct) => TryExtractQuerySystem((ClassDeclarationSyntax)ctx.Node, ctx.SemanticModel, ct))
            .Where(static c => c.Candidate is not null || c.Diagnostic is not null)
            .WithTrackingName("QuerySystemCandidate");

        var collectedChains = chainCandidates.Collect();
        var collectedQuerySystems = querySystemCandidates.Collect();
        var combined = collectedChains.Combine(collectedQuerySystems);

        context.RegisterSourceOutput(combined, static (spc, input) =>
        {
            var (chainResults, querySystemResults) = input;

            foreach (var result in chainResults)
                if (result.Diagnostic is not null) spc.ReportDiagnostic(result.Diagnostic);
            foreach (var result in querySystemResults)
                if (result.Diagnostic is not null) spc.ReportDiagnostic(result.Diagnostic);

            var chains = chainResults
                .Where(c => c.Shape is not null)
                .Select(c => (Shape: c.Shape!, c.SystemTypeName))
                .ToImmutableArray();
            var querySystems = querySystemResults
                .Where(s => s.Candidate is not null)
                .Select(s => s.Candidate!)
                .ToImmutableArray();

            var (byExactShape, byDedupKey) = DeduplicateShapes(spc, chains, querySystems);

            EmitBackends(spc, byDedupKey);
            EmitOverloads(spc, byExactShape);
            EmitQuerySystemGlue(spc, querySystems);
            EmitSystemAccessRegistry(spc, chains, querySystems);
        });
    }

    /// <summary>One <c>.ForEach</c>/<c>.ParallelForEach</c> syntax node's extraction result: either a real <see cref="QueryShape"/>, or a <see cref="Diagnostic"/> explaining why one could not be produced (currently only <see cref="WyrdDiagnostics.FileLocalComponentType"/> reaches this path deliberately; every other unrecognized shape stays silent, since it is not this generator's job to explain every possible reason a chain does not resolve).</summary>
    private readonly record struct ChainCandidateResult(QueryShape? Shape, string? SystemTypeName, Diagnostic? Diagnostic);

    private static ChainCandidateResult ExtractChainCandidate(InvocationExpressionSyntax invocation, SemanticModel semanticModel, CancellationToken ct)
    {
        // Checked before ChainWalker.TryExtractShape: a file-local component type must
        // never reach the exact-shape/dedup pipeline, since that's what lets it silently
        // collide with an unrelated, ordinarily-scoped type of the same simple name
        // elsewhere in the compilation (see WYRD004's own doc comment).
        if (invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiverExpr }
            && semanticModel.GetTypeInfo(receiverExpr, ct).Type is INamedTypeSymbol receiverType
            && ChainWalker.TryFindFileLocalComponentType(receiverType, ct) is { } fileLocalTypeName)
        {
            return new ChainCandidateResult(null, null, Diagnostic.Create(WyrdDiagnostics.FileLocalComponentType, invocation.GetLocation(), fileLocalTypeName));
        }

        var shape = ChainWalker.TryExtractShape(invocation, semanticModel, ct);
        var systemTypeName = shape is null ? null : ChainWalker.TryFindEnclosingSystemType(invocation, semanticModel, ct);
        return new ChainCandidateResult(shape, systemTypeName, null);
    }

    /// <summary>
    /// Two-level grouping: chains/querySystems collapse to one shape per distinct
    /// <see cref="QueryShape.ExactShapeTypeName"/> (one overload each), nested inside one
    /// per distinct <see cref="QueryShapeExtensions.DedupKey"/> (one shared backend each).
    /// Since <c>.Without</c>/<c>.Has</c>/<c>.Any</c> don't affect <c>TShape</c>, two
    /// otherwise-unrelated queries can share the exact same closed <c>Query&lt;TShape&gt;</c>
    /// type while resolving different ref/in markers for it. Emitting both as separate
    /// overloads compiles but produces a hard-to-diagnose CS0121 "ambiguous call" at every
    /// consumer call site, since C# reports both as equally applicable rather than
    /// rejecting the wrong one. So conflicting groups are reported via
    /// <see cref="WyrdDiagnostics.ConflictingAccessForSameShape"/> instead, keeping only
    /// the first shape encountered per <see cref="QueryShape.ExactShapeTypeName"/>.
    /// </summary>
    private static (List<QueryShape> ByExactShape, List<QueryShape> ByDedupKey) DeduplicateShapes(
        SourceProductionContext spc,
        ImmutableArray<(QueryShape Shape, string? SystemTypeName)> chains,
        ImmutableArray<QuerySystemCandidate> querySystems)
    {
        var allShapes = chains.Select(c => c.Shape).Concat(querySystems.Select(s => s.Shape));

        var byExactShape = new List<QueryShape>();
        foreach (var group in allShapes.GroupBy(s => s.ExactShapeTypeName))
        {
            var distinctShapes = group.Distinct().ToList();
            if (distinctShapes.Count > 1)
                spc.ReportDiagnostic(Diagnostic.Create(WyrdDiagnostics.ConflictingAccessForSameShape, Location.None, group.Key));

            byExactShape.Add(distinctShapes[0]);
        }

        var byDedupKey = byExactShape
            .GroupBy(s => s.DedupKey())
            .Select(g => g.First())
            .ToList();

        return (byExactShape, byDedupKey);
    }

    private static void EmitBackends(SourceProductionContext spc, IEnumerable<QueryShape> byDedupKey)
    {
        foreach (var shape in byDedupKey)
            spc.AddSource($"QueryChainBackend.{shape.HashName()}.g.cs", QueryChainEmitter.RenderBackend(shape));
    }

    private static void EmitOverloads(SourceProductionContext spc, IEnumerable<QueryShape> byExactShape)
    {
        foreach (var shape in byExactShape)
        {
            spc.AddSource($"QueryChainForEach.{QueryChainEmitter.ExactShapeHash(shape)}.g.cs", QueryChainEmitter.RenderForEachOverload(shape));
            spc.AddSource($"QueryChainPredicateForEach.{QueryChainEmitter.ExactShapeHash(shape)}.g.cs", QueryChainEmitter.RenderPredicateForEachOverload(shape));
            spc.AddSource($"QueryChainParallelForEach.{QueryChainEmitter.ExactShapeHash(shape)}.g.cs", QueryChainEmitter.RenderParallelForEachOverload(shape));
        }
    }

    private static void EmitQuerySystemGlue(SourceProductionContext spc, ImmutableArray<QuerySystemCandidate> querySystems)
    {
        foreach (var system in querySystems)
            spc.AddSource($"QuerySystem.{system.Namespace}.{system.ClassName}.g.cs", QueryChainEmitter.RenderQuerySystemGlue(system));
    }

    private static void EmitSystemAccessRegistry(
        SourceProductionContext spc,
        ImmutableArray<(QueryShape Shape, string? SystemTypeName)> chains,
        ImmutableArray<QuerySystemCandidate> querySystems)
    {
        var accessFromChains = chains
            .Where(c => c.SystemTypeName is not null)
            .Select(c => (SystemTypeName: c.SystemTypeName!, c.Shape));
        var accessFromQuerySystems = querySystems
            .Select(s => (SystemTypeName: s.Namespace.Length > 0 ? $"{s.Namespace}.{s.ClassName}" : s.ClassName, s.Shape));

        var bySystemType = accessFromChains.Concat(accessFromQuerySystems)
            .GroupBy(c => c.SystemTypeName)
            .Select(g => (
                SystemTypeName: g.Key,
                Reads: g.SelectMany(c => c.Shape.DataElements().Where(m => m.Kind == MarkerKind.Reads).Select(m => m.ComponentTypeName)).Distinct().OrderBy(n => n, System.StringComparer.Ordinal).ToList(),
                Writes: g.SelectMany(c => c.Shape.DataElements().Where(m => m.Kind == MarkerKind.Writes).Select(m => m.ComponentTypeName)).Distinct().OrderBy(n => n, System.StringComparer.Ordinal).ToList()))
            .ToList();

        spc.AddSource("GeneratedSystemAccess.g.cs", QueryChainEmitter.RenderSystemAccessRegistry(bySystemType));
    }

    /// <summary>One <c>QuerySystem</c> class syntax node's extraction result: either a real <see cref="QuerySystemCandidate"/>, or a <see cref="Diagnostic"/> explaining why one could not be produced. Same rationale as <see cref="ChainCandidateResult"/>: only the file-local check reaches this path deliberately; every other "not a valid QuerySystem" reason stays silent.</summary>
    private readonly record struct QuerySystemResult(QuerySystemCandidate? Candidate, Diagnostic? Diagnostic);

    private static QuerySystemResult TryExtractQuerySystem(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, CancellationToken ct)
    {
        if (semanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol classSymbol) return default;
        if (classSymbol.ContainingType is not null) return default; // nested classes not supported
        if (classSymbol.BaseType is not { Name: "QuerySystem" } baseType) return default;
        if (baseType.ContainingNamespace?.ToDisplayString() != "Wyrd.Ecs") return default;

        // Find the real override of QuerySystem.DefineQuery: a genuine abstract member,
        // so this is a symbol comparison against the one specific member, not a
        // name/static/parameter-shape guess that could false-positive on an unrelated
        // method.
        var defineQueryOnBase = baseType.GetMembers("DefineQuery").OfType<IMethodSymbol>().FirstOrDefault();
        if (defineQueryOnBase is null) return default;

        var defineQueryMethod = classSymbol.GetMembers("DefineQuery").OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsOverride && SymbolEqualityComparer.Default.Equals(m.OverriddenMethod?.OriginalDefinition, defineQueryOnBase));
        if (defineQueryMethod is null) return default;

        // Read the shape from DefineQuery's return *expression*, not its declared return
        // type: lets DefineQuery declare the non-generic IQuery instead of restating
        // the exact tuple shape. Only a single-expression body (arrow or
        // block-with-one-return) is recognized; anything else falls through to the
        // ordinary "does not implement abstract member Execute" compiler error the same
        // as a missing DefineQuery would.
        if (defineQueryMethod.DeclaringSyntaxReferences is not [var defineQuerySyntaxRef, ..]) return default;
        if (defineQuerySyntaxRef.GetSyntax(ct) is not MethodDeclarationSyntax { ExpressionBody.Expression: var returnExpr }) return default;

        var defineQuerySemanticModel = semanticModel.Compilation.GetSemanticModel(defineQuerySyntaxRef.SyntaxTree);
        if (defineQuerySemanticModel.GetTypeInfo(returnExpr, ct).Type is not INamedTypeSymbol returnType) return default;

        // Same reasoning and same diagnostic as ExtractChainCandidate: checked before shape
        // extraction, so a file-local component type never reaches the exact-shape/dedup
        // pipeline at all.
        if (ChainWalker.TryFindFileLocalComponentType(returnType, ct) is { } fileLocalTypeName)
            return new QuerySystemResult(null, Diagnostic.Create(WyrdDiagnostics.FileLocalComponentType, returnExpr.GetLocation(), fileLocalTypeName));

        var shape = ChainWalker.TryExtractShapeFromQueryType(returnType, ct);
        if (shape is null) return default;

        var namespaceName = classSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : "";

        // Update is name-convention-recognized, not a real override: its parameter list
        // depends on unpacking an arbitrary TShape tuple, which isn't expressible as a
        // fixed C# signature (see QuerySystem.cs's doc comment). A missing/malformed
        // Update falls through to WYRD002, not this method returning nothing for a reason a
        // developer can't see, so this only bails out here for "no method named Update
        // exists at all" (the true "nothing to resolve against" case, for both the
        // filter-only and data-bearing shapes below); count/type/order mismatches against
        // an Update that *does* exist are WYRD002's job, checked separately by its
        // analyzer, not blocked here.
        var updateMethod = classSymbol.GetMembers("Update").OfType<IMethodSymbol>().FirstOrDefault(m => !m.IsStatic);
        if (updateMethod is null) return default;

        var classification = QuerySystemUpdateShape.Classify(updateMethod.Parameters);
        if (!classification.IsValid) return default;

        if (shape.PendingDataElements.IsEmpty)
        {
            if (updateMethod.Parameters.Length != classification.ComponentStartIndex) return default; // no data parameters beyond Time/World/EntityView

            return new QuerySystemResult(new QuerySystemCandidate
            {
                Namespace = namespaceName,
                ClassName = classSymbol.Name,
                Shape = shape,
                HasWorldParameter = classification.HasWorld,
                HasEntityViewParameter = classification.HasEntityView,
            }, null);
        }

        var dataParameters = updateMethod.Parameters.Skip(classification.ComponentStartIndex).ToImmutableArray();
        if (dataParameters.Length != shape.PendingDataElements.Length) return default;

        var refKinds = dataParameters.Select(p => p.RefKind).ToImmutableArray();
        var resolvedShape = ChainWalker.ResolveAccessKinds(shape, refKinds);
        if (resolvedShape is null) return default;

        return new QuerySystemResult(new QuerySystemCandidate
        {
            Namespace = namespaceName,
            ClassName = classSymbol.Name,
            Shape = resolvedShape,
            HasWorldParameter = classification.HasWorld,
            HasEntityViewParameter = classification.HasEntityView,
        }, null);
    }
}
