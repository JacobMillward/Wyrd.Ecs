using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Persistence.Json.Generators;

/// <summary>
/// Scans for every <c>struct</c> implementing <c>Wyrd.Ecs.IComponent</c> not marked
/// <see cref="Wyrd.Ecs.Persistence.PersistenceIgnoreAttribute"/>, and emits two things into the
/// referencing project's own compilation:
/// <list type="bullet">
/// <item><c>JsonAutoRegistration.RegisterAll</c>: one
/// <c>CodecRegistry.Register&lt;T&gt;</c> call per match, using
/// <c>JsonSerializer.SerializeToUtf8Bytes</c>/<c>Deserialize</c> against the
/// <c>JsonTypeInfo&lt;T&gt;</c> <see cref="JsonContextEmitTask"/> materializes onto
/// <c>&lt;ConsumerName&gt;JsonPersistenceContext.Default</c>.</item>
/// <item>A one-argument <c>WorldBuilder.AddJsonPersistence(IPersistenceStore)</c>/
/// <c>AddJsonPersistence(string)</c> pair delegating to the two-argument
/// <c>AddJsonPersistence(store, registry)</c>.</item>
/// </list>
/// References the context class only by the <see cref="ConsumerContextNaming"/>
/// convention <see cref="JsonContextEmitTask"/> uses, never its generated syntax, so
/// there's no cross-generator ordering dependency despite both targeting the same file.
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
        if (symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Wyrd.Ecs.Persistence.PersistenceIgnoreAttribute")) return null;

        var stableName = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Wyrd.Ecs.StableNameAttribute")
            ?.ConstructorArguments[0].Value as string;
        var discriminator = stableName ?? symbol.ToDisplayString();

        var renamedFrom = symbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == "Wyrd.Ecs.RenamedFromAttribute")
            .Select(a => (string)a.ConstructorArguments[0].Value!)
            .ToImmutableArray();

        return new RegisteredComponentInfo(symbol.ToDisplayString(), discriminator, renamedFrom);
    }

    private static string Render(ImmutableArray<RegisteredComponentInfo> infos, string assemblyName)
    {
        var contextClassName = ConsumerContextNaming.ContextClassName(assemblyName);

        var sb = new StringBuilder();
        sb.AppendLine("namespace Wyrd.Ecs.Persistence.Json;");
        sb.AppendLine();
        sb.AppendLine("public static class JsonAutoRegistration");
        sb.AppendLine("{");
        sb.AppendLine("    public static void RegisterAll(global::Wyrd.Ecs.CodecRegistry registry)");
        sb.AppendLine("    {");

        foreach (var info in infos)
        {
            var typeName = info.FullyQualifiedName;
            var propertyName = ConsumerContextNaming.TypeInfoPropertyName(typeName);
            sb.AppendLine($"        registry.Register<global::{typeName}>(\"{info.Discriminator}\",");
            sb.AppendLine($"            v => global::System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(v, global::{contextClassName}.Default.{propertyName}),");
            sb.AppendLine($"            bytes => global::System.Text.Json.JsonSerializer.Deserialize(bytes, global::{contextClassName}.Default.{propertyName}));");
            foreach (var old in info.RenamedFrom)
                sb.AppendLine($"        registry.RegisterAlias(\"{old}\", \"{info.Discriminator}\");");
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
        sb.AppendLine("            var registry = new global::Wyrd.Ecs.CodecRegistry();");
        sb.AppendLine("            JsonAutoRegistration.RegisterAll(registry);");
        sb.AppendLine("            global::Wyrd.Ecs.Persistence.Generated.TagPersistenceAutoRegistration.RegisterAll(registry);");
        sb.AppendLine("            return builder.AddJsonPersistence(store, registry);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public global::Wyrd.Ecs.WorldBuilder AddJsonPersistence(string path)");
        sb.AppendLine("        {");
        sb.AppendLine("            var registry = new global::Wyrd.Ecs.CodecRegistry();");
        sb.AppendLine("            JsonAutoRegistration.RegisterAll(registry);");
        sb.AppendLine("            global::Wyrd.Ecs.Persistence.Generated.TagPersistenceAutoRegistration.RegisterAll(registry);");
        sb.AppendLine("            return builder.AddJsonPersistence(path, registry);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private record struct RegisteredComponentInfo(string FullyQualifiedName, string Discriminator, ImmutableArray<string> RenamedFrom);
}
