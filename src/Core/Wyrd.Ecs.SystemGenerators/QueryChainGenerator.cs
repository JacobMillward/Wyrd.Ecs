using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.SystemGenerators;

/// <summary>
/// Finds every `.ForEach`/`.ParallelForEach` terminal call on a fluent query chain
/// (<c>world.Query().With&lt;...&gt;()....ForEach(...)</c>) anywhere in the consuming
/// project's source, extracts each one's shape (<see cref="ChainWalker"/>), and emits
/// bespoke terminal methods plus a <c>GeneratedSystemAccess</c> registry entry for
/// chains found directly inside an <c>EcsSystem.OnUpdate</c> override. See the
/// design's "The terminal methods only exist because a generator emits them" and
/// "Canonical parameter order" for the two-level grouping this <see cref="Initialize"/>
/// pipeline implements: exact declaration-order tuple type (one extension-method
/// overload each) nested inside logical shape (one shared backend each).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class QueryChainGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
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

        var collected = candidates.Collect();

        context.RegisterSourceOutput(collected, static (spc, candidates) =>
        {
            var byExactShape = candidates
                .GroupBy(c => c.Shape.ExactShapeTypeName)
                .Select(g => g.First().Shape)
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

            var bySystemType = candidates
                .Where(c => c.SystemTypeName is not null)
                .GroupBy(c => c.SystemTypeName!)
                .Select(g => (
                    SystemTypeName: g.Key,
                    Reads: g.SelectMany(c => c.Shape.DataElements().Where(m => m.Kind == MarkerKind.Reads).Select(m => m.ComponentTypeName)).Distinct().OrderBy(n => n, System.StringComparer.Ordinal).ToList(),
                    Writes: g.SelectMany(c => c.Shape.DataElements().Where(m => m.Kind == MarkerKind.Writes).Select(m => m.ComponentTypeName)).Distinct().OrderBy(n => n, System.StringComparer.Ordinal).ToList()))
                .ToList();

            spc.AddSource("GeneratedSystemAccess.g.cs", QueryChainEmitter.RenderSystemAccessRegistry(bySystemType));
        });
    }
}
