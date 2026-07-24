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
///
/// <see cref="TryExtract"/> pulls every piece of semantic-model-derived data into a
/// <see cref="GeneratedSystemInfo"/> immediately, as plain strings and an int, rather
/// than carrying the <see cref="SemanticModel"/> or any symbol forward. Both
/// <see cref="SemanticModel"/> and most <see cref="ISymbol"/> implementations lack
/// structural equality across two different compilations, so passing either through
/// an incremental pipeline stage defeats Roslyn's step-to-step memoization even
/// without <c>Collect()</c> in the mix; a plain record struct of strings restores it.
/// No <c>Collect()</c> is used at all here since each candidate maps to its own
/// independent output file, so <c>RegisterSourceOutput</c> runs (and caches) per item.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class QuerySystemInliningGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, _) => TryExtract((ClassDeclarationSyntax)ctx.Node, ctx.SemanticModel))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value)
            .WithTrackingName("GeneratedSystemInfo");

        context.RegisterSourceOutput(candidates, static (spc, info) =>
            spc.AddSource($"{info.HintName}.OnUpdate.g.cs", Render(info)));
    }

    private static GeneratedSystemInfo? TryExtract(ClassDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        if (semanticModel.GetDeclaredSymbol(declaration) is not { } symbol) return null;
        if (!IsQuerySystem(symbol.BaseType, out var componentTypes)) return null;
        if (!TryGetContainingChain(symbol, out var containingChain)) return null;

        var execute = symbol.GetMembers("Execute").OfType<IMethodSymbol>().FirstOrDefault(m => m.IsOverride);
        if (execute is null) return null;
        if (execute.DeclaringSyntaxReferences.Length == 0) return null;
        if (execute.DeclaringSyntaxReferences[0].GetSyntax() is not MethodDeclarationSyntax executeSyntax) return null;

        var bodyText = executeSyntax.Body is { } block
            ? string.Join("\n", block.Statements.Select(s => s.ToFullString()))
            : executeSyntax.ExpressionBody is { } arrow
                ? arrow.Expression.ToFullString() + ";"
                : null;
        if (bodyText is null) return null;

        var worldParam = execute.Parameters[0].Name;
        var tickParam = execute.Parameters[1].Name;
        var componentParams = execute.Parameters.Skip(2).ToArray();
        var componentTypeNames = componentTypes.Select(t => t.ToDisplayString()).ToArray();

        var openWrappers = new StringBuilder();
        foreach (var containing in containingChain)
        {
            openWrappers.AppendLine($"partial class {containing.Name}");
            openWrappers.AppendLine("{");
        }

        var bindings = new StringBuilder();
        for (var i = 0; i < componentParams.Length; i++)
            bindings.AppendLine($"            ref var {componentParams[i].Name} = ref __row.Get<{componentTypeNames[i]}>();");

        var hintName = string.Join(".", containingChain.Select(c => c.Name).Append(symbol.Name));

        return new GeneratedSystemInfo(
            HintName: hintName,
            Namespace: symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString(),
            ContainingWrappersOpen: openWrappers.ToString(),
            ContainingWrapperCount: containingChain.Count,
            ClassName: symbol.Name,
            WorldParam: worldParam,
            TickParam: tickParam,
            TypeArgsJoined: string.Join(", ", componentTypeNames),
            ComponentBindings: bindings.ToString(),
            BodyText: bodyText);
    }

    private static string Render(GeneratedSystemInfo info)
    {
        var sb = new StringBuilder();
        if (info.Namespace is not null)
        {
            sb.AppendLine($"namespace {info.Namespace};");
            sb.AppendLine();
        }
        sb.Append(info.ContainingWrappersOpen);
        sb.AppendLine($"partial class {info.ClassName}");
        sb.AppendLine("{");
        sb.AppendLine($"    protected override void OnUpdate(global::Wyrd.Ecs.World {info.WorldParam}, ulong {info.TickParam})");
        sb.AppendLine("    {");
        sb.AppendLine($"        foreach (var __row in {info.WorldParam}.Query<{info.TypeArgsJoined}>())");
        sb.AppendLine("        {");
        sb.Append(info.ComponentBindings);
        sb.AppendLine(info.BodyText);
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        for (var i = 0; i < info.ContainingWrapperCount; i++)
            sb.AppendLine("}");
        return sb.ToString();
    }

    private record struct GeneratedSystemInfo(
        string HintName,
        string? Namespace,
        string ContainingWrappersOpen,
        int ContainingWrapperCount,
        string ClassName,
        string WorldParam,
        string TickParam,
        string TypeArgsJoined,
        string ComponentBindings,
        string BodyText);

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
