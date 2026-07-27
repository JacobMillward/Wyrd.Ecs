using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Generators;

/// <summary>
/// Finds every `.ForEach`/`.ParallelForEach` terminal call on a fluent query chain, and
/// every `QuerySystem` subclass's `Build` method, anywhere in the consuming project's
/// source. Extracts each one's shape (<see cref="ChainWalker"/>) and emits bespoke
/// terminal methods, `QuerySystem` glue, and a `GeneratedSystemAccess` registry entry
/// for both chains found directly inside an `EcsSystem.OnUpdate` override and
/// `QuerySystem` subclasses. See the design's "The terminal methods only exist because
/// a generator emits them" and "Canonical parameter order" for the two-level grouping
/// this <see cref="Initialize"/> pipeline implements for chain terminals: exact
/// declaration-order tuple type (one extension-method overload each) nested inside
/// logical shape (one shared backend each).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class QueryChainGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var chainCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax
                    {
                        Name: IdentifierNameSyntax { Identifier.ValueText: "ForEach" or "ParallelForEach" }
                    }
                },
                transform: static (ctx, ct) =>
                {
                    var invocation = (InvocationExpressionSyntax)ctx.Node;
                    var shape = ChainWalker.TryExtractShape(invocation, ctx.SemanticModel, ct);
                    var systemTypeName = shape is null ? null : ChainWalker.TryFindEnclosingSystemType(invocation, ctx.SemanticModel, ct);
                    return (Shape: shape, SystemTypeName: systemTypeName);
                })
            .Where(static c => c.Shape is not null)
            .Select(static (c, _) => (Shape: c.Shape!, c.SystemTypeName))
            .WithTrackingName("QueryChainShape");

        var querySystemCandidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList.Types.Count: > 0 },
                transform: static (ctx, ct) => TryExtractQuerySystem((ClassDeclarationSyntax)ctx.Node, ctx.SemanticModel, ct))
            .Where(static c => c is not null)
            .Select(static (c, _) => c!)
            .WithTrackingName("QuerySystemCandidate");

        var collectedChains = chainCandidates.Collect();
        var collectedQuerySystems = querySystemCandidates.Collect();
        var combined = collectedChains.Combine(collectedQuerySystems);

        context.RegisterSourceOutput(combined, static (spc, input) =>
        {
            var (chains, querySystems) = input;

            var byExactShape = chains.Select(c => c.Shape).Concat(querySystems.Select(s => s.Shape))
                .GroupBy(s => s.ExactShapeTypeName)
                .Select(g => g.First())
                .ToList();

            var byDedupKey = byExactShape
                .GroupBy(s => s.DedupKey())
                .Select(g => g.First())
                .ToList();

            foreach (var shape in byDedupKey)
                spc.AddSource($"QueryChainBackend.{shape.HashName()}.g.cs", QueryChainEmitter.RenderBackend(shape));

            foreach (var shape in byExactShape)
            {
                spc.AddSource($"QueryChainForEach.{QueryChainEmitter.ExactShapeHash(shape)}.g.cs", QueryChainEmitter.RenderForEachOverload(shape));
                spc.AddSource($"QueryChainPredicateForEach.{QueryChainEmitter.ExactShapeHash(shape)}.g.cs", QueryChainEmitter.RenderPredicateForEachOverload(shape));
                spc.AddSource($"QueryChainParallelForEach.{QueryChainEmitter.ExactShapeHash(shape)}.g.cs", QueryChainEmitter.RenderParallelForEachOverload(shape));
            }

            foreach (var system in querySystems)
                spc.AddSource($"QuerySystem.{system.Namespace}.{system.ClassName}.g.cs", QueryChainEmitter.RenderQuerySystemGlue(system));

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
        });
    }

    private static QuerySystemCandidate? TryExtractQuerySystem(ClassDeclarationSyntax classDecl, SemanticModel semanticModel, CancellationToken ct)
    {
        if (semanticModel.GetDeclaredSymbol(classDecl, ct) is not INamedTypeSymbol classSymbol) return null;
        if (classSymbol.ContainingType is not null) return null; // nested classes not supported -- see Task 11's context note.
        if (classSymbol.BaseType is not { Name: "QuerySystem" } baseType) return null;
        if (baseType.ContainingNamespace?.ToDisplayString() != "Wyrd.Ecs") return null;

        var buildMethod = classSymbol.GetMembers("Build").OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsStatic && m.Parameters is [{ Type.Name: "World" }]);
        if (buildMethod is null) return null;

        // Read the shape from Build's return *expression*, not its declared return
        // type -- lets Build declare the non-generic IQueryDefinition instead of
        // restating the exact tuple shape. Only a single-expression body (arrow or
        // block-with-one-return) is recognized; anything else is treated the same as
        // any other unrecognized Build shape (falls through to the ordinary "does not
        // implement abstract member OnUpdate" compiler error).
        if (buildMethod.DeclaringSyntaxReferences is not [var buildSyntaxRef, ..]) return null;
        if (buildSyntaxRef.GetSyntax(ct) is not MethodDeclarationSyntax { ExpressionBody.Expression: var returnExpr }) return null;

        var buildSemanticModel = semanticModel.Compilation.GetSemanticModel(buildSyntaxRef.SyntaxTree);
        if (buildSemanticModel.GetTypeInfo(returnExpr, ct).Type is not INamedTypeSymbol returnType) return null;

        var shape = ChainWalker.TryExtractShapeFromQueryType(returnType, ct);
        if (shape is null) return null;

        return new QuerySystemCandidate
        {
            Namespace = classSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : "",
            ClassName = classSymbol.Name,
            Shape = shape,
        };
    }
}
