using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.SystemGenerators;

/// <summary>
/// For every class deriving from <c>Wyrd.Ecs.QuerySystem&lt;T0..T7&gt;</c>, emits
/// <c>OnUpdate</c> as a second partial declaration: a loop over
/// <c>world.Query&lt;T0..T7&gt;()</c> with the class's own <c>Execute</c> override's
/// body copied in verbatim underneath per-component <c>ref var</c> locals named to
/// match <c>Execute</c>'s own parameter names, so no identifier rewriting is needed.
/// Supports classes nested inside another type by wrapping the generated
/// declaration in matching <c>partial class</c> wrappers for every containing type,
/// as long as none of them are generic. If the class (or any containing type) isn't
/// declared <c>partial</c>, the native compiler already reports a clear, correctly
/// localized <c>CS0260</c>; this generator doesn't need its own diagnostic for that
/// case.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class QuerySystemInliningGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
            transform: static (ctx, _) => (Declaration: (ClassDeclarationSyntax)ctx.Node, ctx.SemanticModel));

        context.RegisterSourceOutput(candidates.Collect(), static (spc, items) =>
        {
            foreach (var (declaration, semanticModel) in items)
            {
                if (semanticModel.GetDeclaredSymbol(declaration) is not { } symbol) continue;
                if (!IsQuerySystem(symbol.BaseType, out var componentTypes)) continue;
                if (!TryGetContainingChain(symbol, out var containingChain)) continue;

                var execute = symbol.GetMembers("Execute").OfType<IMethodSymbol>().FirstOrDefault(m => m.IsOverride);
                if (execute is null) continue;
                if (execute.DeclaringSyntaxReferences.Length == 0) continue;
                if (execute.DeclaringSyntaxReferences[0].GetSyntax() is not MethodDeclarationSyntax executeSyntax) continue;

                var bodyText = executeSyntax.Body is { } block
                    ? string.Join("\n", block.Statements.Select(s => s.ToFullString()))
                    : executeSyntax.ExpressionBody is { } arrow
                        ? arrow.Expression.ToFullString() + ";"
                        : null;
                if (bodyText is null) continue;

                var worldParam = execute.Parameters[0].Name;
                var tickParam = execute.Parameters[1].Name;
                var componentParams = execute.Parameters.Skip(2).ToArray();

                var typeArgs = string.Join(", ", componentTypes.Select(t => t.ToDisplayString()));
                var sb = new StringBuilder();
                if (!symbol.ContainingNamespace.IsGlobalNamespace)
                {
                    sb.AppendLine($"namespace {symbol.ContainingNamespace.ToDisplayString()};");
                    sb.AppendLine();
                }
                foreach (var containing in containingChain)
                {
                    sb.AppendLine($"partial class {containing.Name}");
                    sb.AppendLine("{");
                }
                sb.AppendLine($"partial class {symbol.Name}");
                sb.AppendLine("{");
                sb.AppendLine($"    protected override void OnUpdate(global::Wyrd.Ecs.World {worldParam}, ulong {tickParam})");
                sb.AppendLine("    {");
                sb.AppendLine($"        foreach (var __row in {worldParam}.Query<{typeArgs}>())");
                sb.AppendLine("        {");
                for (var i = 0; i < componentParams.Length; i++)
                    sb.AppendLine($"            ref var {componentParams[i].Name} = ref __row.Get<{componentTypes[i].ToDisplayString()}>();");
                sb.AppendLine(bodyText);
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                for (var i = 0; i < containingChain.Count; i++)
                    sb.AppendLine("}");

                var hintName = string.Join(".", containingChain.Select(c => c.Name).Append(symbol.Name));
                spc.AddSource($"{hintName}.OnUpdate.g.cs", sb.ToString());
            }
        });
    }

    /// <summary>
    /// Walks <paramref name="symbol"/>'s <see cref="INamedTypeSymbol.ContainingType"/>
    /// chain, outermost first. Returns <c>false</c> (skip generation for this class)
    /// if any containing type is generic, since replicating a containing type's own
    /// type parameter list correctly is out of scope for this generator.
    /// </summary>
    private static bool TryGetContainingChain(INamedTypeSymbol symbol, out List<INamedTypeSymbol> chain)
    {
        chain = new List<INamedTypeSymbol>();
        var current = symbol.ContainingType;
        while (current is not null)
        {
            if (current.TypeParameters.Length > 0) return false;
            chain.Add(current);
            current = current.ContainingType;
        }
        chain.Reverse();
        return true;
    }

    private static bool IsQuerySystem(INamedTypeSymbol? type, out ImmutableArray<ITypeSymbol> componentTypes)
    {
        componentTypes = default;
        if (type is null) return false;
        var original = type.OriginalDefinition;
        if (original.Name != "QuerySystem" || original.ContainingNamespace?.ToDisplayString() != "Wyrd.Ecs") return false;

        componentTypes = type.TypeArguments;
        return true;
    }
}
