using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Persistence.Binary.Generators;

/// <summary>
/// Scans for every <c>struct</c> implementing <c>Wyrd.Ecs.IComponent</c> and marked
/// <c>[MemoryPackable]</c>, and emits two things into the referencing project's own
/// compilation:
/// <list type="bullet">
/// <item><c>MemoryPackAutoRegistration.RegisterAll</c>: one
/// <c>CodecRegistry.Register&lt;T&gt;</c> call per match, using
/// <c>MemoryPackSerializer.Serialize</c>/<c>Deserialize&lt;T&gt;</c>.</item>
/// <item>A one-argument <c>WorldBuilder.AddBinaryPersistence(IPersistenceStore)</c>/
/// <c>AddBinaryPersistence(string)</c> pair that builds a registry, calls
/// <c>RegisterAll</c>, and delegates to the two-argument
/// <c>AddBinaryPersistence(store, registry)</c>.</item>
/// </list>
/// Discriminators are each type's fully qualified name, so two same-named components in
/// different namespaces don't collide.
///
/// <see cref="TryExtract"/> pulls only the fully-qualified type name out of the semantic
/// model immediately, rather than carrying <see cref="INamedTypeSymbol"/> through
/// <c>Collect()</c>: symbols don't compare structurally equal across compilations, so
/// keeping one in the pipeline defeats incremental caching for the whole file.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class MemoryPackRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is StructDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, _) => TryExtract((StructDeclarationSyntax)ctx.Node, ctx.SemanticModel))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value)
            .WithTrackingName("RegisteredComponentInfo");

        context.RegisterSourceOutput(candidates.Collect(), static (spc, infos) =>
            spc.AddSource("MemoryPackAutoRegistration.g.cs", Render(infos)));
    }

    private static RegisteredComponentInfo? TryExtract(StructDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        if (semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) return null;
        if (!IsComponent(symbol)) return null;
        if (!HasMemoryPackableAttribute(symbol)) return null;

        return new RegisteredComponentInfo(symbol.ToDisplayString());
    }

    private static string Render(ImmutableArray<RegisteredComponentInfo> infos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace Wyrd.Ecs.Persistence.Binary;");
        sb.AppendLine();
        sb.AppendLine("public static class MemoryPackAutoRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    public static void RegisterAll(global::Wyrd.Ecs.CodecRegistry registry)");
        sb.AppendLine("    {");

        foreach (var info in infos)
        {
            var typeName = info.FullyQualifiedName;
            sb.AppendLine($"        registry.Register<global::{typeName}>(\"{typeName}\",");
            sb.AppendLine("            v => global::MemoryPack.MemoryPackSerializer.Serialize(v),");
            sb.AppendLine($"            bytes => global::MemoryPack.MemoryPackSerializer.Deserialize<global::{typeName}>(bytes));");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("public static class MemoryPackAutoRegistrationWorldBuilderExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    extension(global::Wyrd.Ecs.WorldBuilder builder)");
        sb.AppendLine("    {");
        sb.AppendLine("        public global::Wyrd.Ecs.WorldBuilder AddBinaryPersistence(global::Wyrd.Ecs.Persistence.IPersistenceStore store)");
        sb.AppendLine("        {");
        sb.AppendLine("            var registry = new global::Wyrd.Ecs.CodecRegistry();");
        sb.AppendLine("            MemoryPackAutoRegistration.RegisterAll(registry);");
        sb.AppendLine("            return builder.AddBinaryPersistence(store, registry);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public global::Wyrd.Ecs.WorldBuilder AddBinaryPersistence(string path)");
        sb.AppendLine("        {");
        sb.AppendLine("            var registry = new global::Wyrd.Ecs.CodecRegistry();");
        sb.AppendLine("            MemoryPackAutoRegistration.RegisterAll(registry);");
        sb.AppendLine("            return builder.AddBinaryPersistence(path, registry);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private record struct RegisteredComponentInfo(string FullyQualifiedName);

    private static bool IsComponent(INamedTypeSymbol symbol) =>
        symbol.AllInterfaces.Any(i => i.ToDisplayString() == "Wyrd.Ecs.IComponent");

    private static bool HasMemoryPackableAttribute(INamedTypeSymbol symbol) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "MemoryPack.MemoryPackableAttribute");
}
