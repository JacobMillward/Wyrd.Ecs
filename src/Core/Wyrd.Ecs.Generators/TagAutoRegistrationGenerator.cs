using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Generators;

/// <summary>
/// Scans for every <c>struct</c> implementing <c>Wyrd.Ecs.ITag</c> and emits
/// <c>Wyrd.Ecs.Generated.TagAutoRegistration.RegisterAll</c>: one
/// <c>ComponentCodecRegistry.RegisterTag&lt;T&gt;</c> call per match, using the bare type
/// name (<c>nameof</c>) as the discriminator. Unlike
/// <c>Wyrd.Ecs.Persistence.Json.Generators.JsonRegistrationGenerator</c> (which uses a
/// fully-qualified name specifically to let same-simple-name types across namespaces
/// coexist), a tag discriminator has no wire-format/schema-hash stakes — it only needs to
/// be unique enough for a debug display label. Two tags sharing a simple name in
/// different namespaces throw at <c>RegisterTag</c> call time (its own duplicate guard),
/// surfaced immediately rather than silently colliding.
///
/// <para>
/// A candidate that <c>RegisterAll</c> (a public top-level class) could never legally
/// reference is silently skipped, not diagnosed: a private nested test-helper struct
/// implementing <c>ITag</c> for some unrelated purpose is not a mistake the way a
/// file-scoped component referenced directly in a query chain is (that case is
/// <c>QueryChainGenerator</c>'s WYRD004) — it's simply not a candidate for this
/// auto-discovery mechanism, the same "unregistered things don't appear" contract the
/// rest of this feature already has.
/// </para>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TagAutoRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => TryExtract((StructDeclarationSyntax)ctx.Node, ctx.SemanticModel))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value)
            .WithTrackingName("RegisteredTagInfo");

        context.RegisterSourceOutput(candidates.Collect(), static (spc, infos) =>
            spc.AddSource("TagAutoRegistration.g.cs", Render(infos)));
    }

    private static RegisteredTagInfo? TryExtract(StructDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        if (semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) return null;
        if (!symbol.AllInterfaces.Any(i => i.ToDisplayString() == "Wyrd.Ecs.ITag")) return null;
        if (symbol.IsFileLocal) return null;
        if (!semanticModel.Compilation.IsSymbolAccessibleWithin(symbol, semanticModel.Compilation.Assembly)) return null;

        return new RegisteredTagInfo(symbol.ToDisplayString(), symbol.Name);
    }

    private static string Render(ImmutableArray<RegisteredTagInfo> infos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace Wyrd.Ecs.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class TagAutoRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    public static void RegisterAll(global::Wyrd.Ecs.ComponentCodecRegistry registry)");
        sb.AppendLine("    {");

        foreach (var info in infos)
            sb.AppendLine($"        registry.RegisterTag<global::{info.FullyQualifiedName}>(\"{info.SimpleName}\");");

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private record struct RegisteredTagInfo(string FullyQualifiedName, string SimpleName);
}
