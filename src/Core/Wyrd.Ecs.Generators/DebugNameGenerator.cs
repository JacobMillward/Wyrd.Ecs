using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Generators;

/// <summary>
/// Scans for every struct implementing Wyrd.Ecs.IComponent, ITag, or IRelation and emits
/// a module initializer that registers each with Internal.DebugNameRegistry under its bare
/// type name. Runs at assembly load, no caller-invoked RegisterAll: a debug display name
/// needs no setup, unlike a persisted discriminator.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DebugNameGenerator : IIncrementalGenerator
{
    private static readonly string[] TargetInterfaces =
    [
        "Wyrd.Ecs.IComponent",
        "Wyrd.Ecs.ITag",
        "Wyrd.Ecs.IRelation",
    ];

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => TryExtract((StructDeclarationSyntax)ctx.Node, ctx.SemanticModel))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value)
            .WithTrackingName("RegisteredDebugNameInfo");

        context.RegisterSourceOutput(candidates.Collect(), static (spc, infos) =>
            spc.AddSource("DebugNames.g.cs", Render(infos)));
    }

    private static RegisteredDebugNameInfo? TryExtract(StructDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        if (semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) return null;
        if (!symbol.AllInterfaces.Any(i => TargetInterfaces.Contains(i.ToDisplayString()))) return null;
        if (symbol.IsFileLocal) return null;
        if (!semanticModel.Compilation.IsSymbolAccessibleWithin(symbol, semanticModel.Compilation.Assembly)) return null;

        return new RegisteredDebugNameInfo(symbol.ToDisplayString(), symbol.Name);
    }

    private static string Render(ImmutableArray<RegisteredDebugNameInfo> infos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace Wyrd.Ecs.Generated;");
        sb.AppendLine();
        sb.AppendLine("internal static class DebugNames");
        sb.AppendLine("{");
        sb.AppendLine("    [System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    internal static void Init()");
        sb.AppendLine("    {");

        foreach (var info in infos)
            sb.AppendLine($"        Wyrd.Ecs.Internal.DebugNameRegistry.Register<global::{info.FullyQualifiedName}>(\"{info.SimpleName}\");");

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private record struct RegisteredDebugNameInfo(string FullyQualifiedName, string SimpleName);
}
