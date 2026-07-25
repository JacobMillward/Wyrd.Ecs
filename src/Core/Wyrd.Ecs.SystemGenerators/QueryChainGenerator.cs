using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.SystemGenerators;

/// <summary>
/// Finds every `.ForEach`/`.ParallelForEach` terminal call on a fluent query chain
/// (<c>world.Query().With&lt;...&gt;()....ForEach(...)</c>) anywhere in the consuming
/// project's source, extracts each one's shape (<see cref="ChainWalker"/>), and emits
/// bespoke terminal methods plus (Task 10) a <c>GeneratedSystemAccess</c> registry
/// entry. See the design's "The terminal methods only exist because a generator emits
/// them" and "Canonical parameter order" for the two-level grouping this
/// <see cref="Initialize"/> pipeline implements: exact declaration-order tuple type
/// (one extension-method overload each) nested inside logical shape (one shared
/// backend each).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class QueryChainGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name: IdentifierNameSyntax { Identifier.ValueText: "ForEach" } }
                },
                transform: static (ctx, ct) => ChainWalker.TryExtractShape((InvocationExpressionSyntax)ctx.Node, ctx.SemanticModel, ct))
            .Where(static shape => shape is not null)
            .Select(static (shape, _) => shape!)
            .WithTrackingName("QueryChainShape");

        var collected = candidates.Collect();

        context.RegisterSourceOutput(collected, static (spc, shapes) =>
        {
            var byExactShape = shapes
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
                spc.AddSource($"QueryChainForEach.{QueryChainEmitter.ExactShapeHash(shape)}.g.cs", QueryChainEmitter.RenderForEachOverload(shape));

            foreach (var shape in byExactShape)
                spc.AddSource($"QueryChainPredicateForEach.{QueryChainEmitter.ExactShapeHash(shape)}.g.cs", QueryChainEmitter.RenderPredicateForEachOverload(shape));
        });
    }
}
