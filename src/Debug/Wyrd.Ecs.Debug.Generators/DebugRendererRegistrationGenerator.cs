using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Debug.Generators;

/// <summary>
/// Scans for every <c>struct</c> implementing <c>Wyrd.Ecs.IComponent</c> and carrying
/// <c>Wyrd.Ecs.Debug.Abstractions.DebugRendererAttribute</c>, and emits a module
/// initializer registering describe/apply delegates per match into
/// <c>Wyrd.Ecs.Debug.DebugRendererRegistry</c>, keyed by the same simple type name
/// <c>Wyrd.Ecs.Generators.DebugNameGenerator</c> registers for the type. References
/// <c>Wyrd.Ecs.Debug.Abstractions.IComponentInspectorRenderer&lt;T&gt;</c> and
/// <c>Wyrd.Ecs.Debug.DebugRendererRegistry</c> by name only, never their compiled syntax,
/// since neither lives in this generator's own compilation (dotnet/roslyn#77560).
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DebugRendererRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => TryExtract((StructDeclarationSyntax)ctx.Node, ctx.SemanticModel))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value)
            .WithTrackingName("DebugRendererRegistrationInfo");

        context.RegisterSourceOutput(candidates.Collect(), static (spc, infos) =>
            spc.AddSource("DebugRendererRegistrations.g.cs", Render(infos)));
    }

    private static DebugRendererInfo? TryExtract(StructDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        if (semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) return null;
        if (!symbol.AllInterfaces.Any(i => i.ToDisplayString() == "Wyrd.Ecs.IComponent")) return null;

        var attribute = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Wyrd.Ecs.Debug.Abstractions.DebugRendererAttribute");
        if (attribute is null) return null;
        if (attribute.ConstructorArguments.Length != 1) return null;
        if (attribute.ConstructorArguments[0].Value is not ITypeSymbol rendererType) return null;

        return new DebugRendererInfo(symbol.ToDisplayString(), symbol.Name, rendererType.ToDisplayString());
    }

    private static string Render(ImmutableArray<DebugRendererInfo> infos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace Wyrd.Ecs.Debug.Generated;");
        sb.AppendLine();
        sb.AppendLine("internal static class DebugRendererRegistrations");
        sb.AppendLine("{");
        sb.AppendLine("    [System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    internal static void Init()");
        sb.AppendLine("    {");

        foreach (var info in infos)
        {
            sb.AppendLine($"        global::Wyrd.Ecs.Debug.DebugRendererRegistry.Register(\"{info.DebugName}\",");
            sb.AppendLine($"            value => new global::{info.RendererType}().Describe((global::{info.ComponentType})value),");
            sb.AppendLine($"            (value, edit) => new global::{info.RendererType}().Apply((global::{info.ComponentType})value, edit));");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private record struct DebugRendererInfo(string ComponentType, string DebugName, string RendererType);
}
