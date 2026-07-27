using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Generators;

/// <summary>
/// Finds every `.ForEach`/`.ParallelForEach` terminal call on a fluent query chain, and
/// every `QuerySystem` subclass's `DefineQuery` override, anywhere in the consuming
/// project's source. Extracts each one's shape (<see cref="ChainWalker"/>) and emits bespoke
/// terminal methods, `QuerySystem` glue, and a `GeneratedSystemAccess` registry entry
/// for both chains found directly inside an `EcsSystem.Execute` override and
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

        // Find the real override of QuerySystem.DefineQuery -- a genuine abstract member,
        // so this is a symbol comparison against the one specific member, not a
        // name/static/parameter-shape guess that could false-positive on an unrelated
        // method.
        var defineQueryOnBase = baseType.GetMembers("DefineQuery").OfType<IMethodSymbol>().FirstOrDefault();
        if (defineQueryOnBase is null) return null;

        var defineQueryMethod = classSymbol.GetMembers("DefineQuery").OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsOverride && SymbolEqualityComparer.Default.Equals(m.OverriddenMethod?.OriginalDefinition, defineQueryOnBase));
        if (defineQueryMethod is null) return null;

        // Read the shape from DefineQuery's return *expression*, not its declared return
        // type -- lets DefineQuery declare the non-generic IQuery instead of restating
        // the exact tuple shape. Only a single-expression body (arrow or
        // block-with-one-return) is recognized; anything else falls through to the
        // ordinary "does not implement abstract member Execute" compiler error the same
        // as a missing DefineQuery would.
        if (defineQueryMethod.DeclaringSyntaxReferences is not [var defineQuerySyntaxRef, ..]) return null;
        if (defineQuerySyntaxRef.GetSyntax(ct) is not MethodDeclarationSyntax { ExpressionBody.Expression: var returnExpr }) return null;

        var defineQuerySemanticModel = semanticModel.Compilation.GetSemanticModel(defineQuerySyntaxRef.SyntaxTree);
        if (defineQuerySemanticModel.GetTypeInfo(returnExpr, ct).Type is not INamedTypeSymbol returnType) return null;

        var shape = ChainWalker.TryExtractShapeFromQueryType(returnType, ct);
        if (shape is null) return null;

        var namespaceName = classSymbol.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : "";

        if (shape.PendingDataElements.IsEmpty)
        {
            return new QuerySystemCandidate
            {
                Namespace = namespaceName,
                ClassName = classSymbol.Name,
                Shape = shape,
            };
        }

        // Update is name-convention-recognized, not a real override -- its parameter list
        // depends on unpacking an arbitrary TShape tuple, which isn't expressible as a
        // fixed C# signature (see QuerySystem.cs's doc comment). A missing/malformed
        // Update falls through to WYRD002, not this method returning null for a reason a
        // developer can't see -- so this only returns null here for "no method named
        // Update exists at all" (the true "nothing to resolve against" case);
        // count/type/order mismatches against an Update that *does* exist are WYRD002's
        // job, checked separately by its analyzer, not blocked here.
        var updateMethod = classSymbol.GetMembers("Update").OfType<IMethodSymbol>().FirstOrDefault(m => !m.IsStatic);
        if (updateMethod is null) return null;

        // Skip Update's leading Time parameter, same convention as the lambda case.
        var dataParameters = updateMethod.Parameters.Skip(1).ToImmutableArray();
        if (dataParameters.Length != shape.PendingDataElements.Length) return null;

        var refKinds = dataParameters.Select(p => p.RefKind).ToImmutableArray();
        var resolvedShape = ChainWalker.ResolveAccessKinds(shape, refKinds);
        if (resolvedShape is null) return null;

        return new QuerySystemCandidate
        {
            Namespace = namespaceName,
            ClassName = classSymbol.Name,
            Shape = resolvedShape,
        };
    }
}
