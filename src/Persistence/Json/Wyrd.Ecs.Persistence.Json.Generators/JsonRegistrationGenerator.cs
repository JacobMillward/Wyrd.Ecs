using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Persistence.Json.Generators;

/// <summary>
/// Scans for every <c>struct</c> implementing <c>Wyrd.Ecs.IComponent</c> not marked
/// <see cref="JsonPersistenceIgnoreAttribute"/>, and emits two things into the
/// referencing project's own compilation:
/// <list type="bullet">
/// <item><c>JsonAutoRegistration.RegisterAll</c>: one
/// <c>ComponentCodecRegistry.Register&lt;T&gt;</c> call per match, using
/// <c>JsonSerializer.SerializeToUtf8Bytes</c>/<c>Deserialize</c> against the
/// <c>JsonTypeInfo&lt;T&gt;</c> <see cref="JsonContextEmitTask"/> materialized onto the
/// project's own <c>&lt;ConsumerName&gt;JsonPersistenceContext.Default</c>.</item>
/// <item>A one-argument <c>WorldBuilder.AddJsonPersistence(IPersistenceStore)</c>/
/// <c>AddJsonPersistence(string)</c> pair delegating to
/// <c>Wyrd.Ecs.Persistence.Json</c>'s own two-argument
/// <c>AddJsonPersistence(store, registry)</c>.</item>
/// </list>
/// This generator only ever references the context class by the same
/// <see cref="ConsumerContextNaming"/> convention <see cref="JsonContextEmitTask"/>
/// used to materialize it — it never inspects that file's own generated syntax, so
/// there's no cross-generator ordering dependency here despite both pieces targeting
/// the same physical output file.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class JsonRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => TryExtract((StructDeclarationSyntax)ctx.Node, ctx.SemanticModel))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value)
            .WithTrackingName("JsonRegisteredComponentInfo");

        var assemblyName = context.CompilationProvider.Select(static (c, _) => c.AssemblyName ?? "Consumer");

        context.RegisterSourceOutput(candidates.Collect().Combine(assemblyName), static (spc, pair) =>
            spc.AddSource("JsonAutoRegistration.g.cs", Render(pair.Left, pair.Right)));
    }

    private static RegisteredComponentInfo? TryExtract(StructDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        if (semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) return null;
        if (!symbol.AllInterfaces.Any(i => i.ToDisplayString() == "Wyrd.Ecs.IComponent")) return null;
        if (symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Wyrd.Ecs.Persistence.Json.JsonPersistenceIgnoreAttribute")) return null;

        return new RegisteredComponentInfo(symbol.ToDisplayString());
    }

    private static string Render(ImmutableArray<RegisteredComponentInfo> infos, string assemblyName)
    {
        var contextClassName = ConsumerContextNaming.ContextClassName(assemblyName);

        var sb = new StringBuilder();
        sb.AppendLine("namespace Wyrd.Ecs.Persistence.Json;");
        sb.AppendLine();
        sb.AppendLine("public static class JsonAutoRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    public static void RegisterAll(global::Wyrd.Ecs.ComponentCodecRegistry registry)");
        sb.AppendLine("    {");

        foreach (var info in infos)
        {
            var typeName = info.FullyQualifiedName;
            var propertyName = ConsumerContextNaming.TypeInfoPropertyName(typeName);
            sb.AppendLine($"        registry.Register<global::{typeName}>(\"{typeName}\",");
            sb.AppendLine($"            v => global::System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(v, global::{contextClassName}.Default.{propertyName}),");
            sb.AppendLine($"            bytes => global::System.Text.Json.JsonSerializer.Deserialize(bytes, global::{contextClassName}.Default.{propertyName}));");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("public static class JsonAutoRegistrationWorldBuilderExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    extension(global::Wyrd.Ecs.WorldBuilder builder)");
        sb.AppendLine("    {");
        sb.AppendLine("        public global::Wyrd.Ecs.WorldBuilder AddJsonPersistence(global::Wyrd.Ecs.Persistence.IPersistenceStore store)");
        sb.AppendLine("        {");
        sb.AppendLine("            var registry = new global::Wyrd.Ecs.ComponentCodecRegistry();");
        sb.AppendLine("            JsonAutoRegistration.RegisterAll(registry);");
        sb.AppendLine("            return builder.AddJsonPersistence(store, registry);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public global::Wyrd.Ecs.WorldBuilder AddJsonPersistence(string path)");
        sb.AppendLine("        {");
        sb.AppendLine("            var registry = new global::Wyrd.Ecs.ComponentCodecRegistry();");
        sb.AppendLine("            JsonAutoRegistration.RegisterAll(registry);");
        sb.AppendLine("            return builder.AddJsonPersistence(path, registry);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private record struct RegisteredComponentInfo(string FullyQualifiedName);
}
