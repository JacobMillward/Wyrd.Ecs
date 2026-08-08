using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Persistence.Generators;

/// <summary>
/// Scans for every struct implementing Wyrd.Ecs.ITag and emits
/// Wyrd.Ecs.Persistence.Generated.TagPersistenceAutoRegistration: one
/// CodecRegistry.RegisterTag call per match, using the fully qualified type name
/// as the discriminator (unlike debug naming's bare simple name - this one has real wire
/// stakes, same convention MemoryPack/Json component registration already uses). No
/// format-specific concern exists for a zero-data marker, so one generator serves both
/// Binary and Json persistence.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TagPersistenceAutoRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => TryExtract((StructDeclarationSyntax)ctx.Node, ctx.SemanticModel))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value)
            .WithTrackingName("RegisteredTagPersistenceInfo");

        context.RegisterSourceOutput(candidates.Collect(), static (spc, infos) =>
            spc.AddSource("TagPersistenceAutoRegistration.g.cs", Render(infos)));
    }

    private static RegisteredTagPersistenceInfo? TryExtract(StructDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        if (semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) return null;
        if (!symbol.AllInterfaces.Any(i => i.ToDisplayString() == "Wyrd.Ecs.ITag")) return null;
        if (symbol.IsFileLocal) return null;
        if (!semanticModel.Compilation.IsSymbolAccessibleWithin(symbol, semanticModel.Compilation.Assembly)) return null;
        if (symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Wyrd.Ecs.Persistence.PersistenceIgnoreAttribute")) return null;

        var stableName = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Wyrd.Ecs.StableNameAttribute")
            ?.ConstructorArguments[0].Value as string;

        var renamedFrom = symbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == "Wyrd.Ecs.RenamedFromAttribute")
            .Select(a => (string)a.ConstructorArguments[0].Value!)
            .ToImmutableArray();

        return new RegisteredTagPersistenceInfo(symbol.ToDisplayString(), stableName ?? symbol.ToDisplayString(), renamedFrom);
    }

    private static string Render(ImmutableArray<RegisteredTagPersistenceInfo> infos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace Wyrd.Ecs.Persistence.Generated;");
        sb.AppendLine();
        sb.AppendLine("public static class TagPersistenceAutoRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    public static void RegisterAll(global::Wyrd.Ecs.CodecRegistry registry)");
        sb.AppendLine("    {");

        foreach (var info in infos)
        {
            sb.AppendLine($"        registry.RegisterTag<global::{info.FullyQualifiedName}>(\"{info.Discriminator}\");");
            foreach (var old in info.RenamedFrom)
                sb.AppendLine($"        registry.RegisterAlias(\"{old}\", \"{info.Discriminator}\");");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private record struct RegisteredTagPersistenceInfo(string FullyQualifiedName, string Discriminator, ImmutableArray<string> RenamedFrom);
}
