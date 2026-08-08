using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Persistence.Binary.Generators;

/// <summary>
/// Scans for every <c>struct</c> implementing <c>Wyrd.Ecs.IComponent</c>, not marked
/// <c>[PersistenceIgnore]</c>, and emits two things into the referencing project's own
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
/// A matched component needs no other annotation if it's unmanaged (MemoryPack's own
/// built-in fast path handles it directly) or already marked <c>[MemoryPackable]</c>
/// (MemoryPack's own generator handles it, unchanged from before this comment). A
/// component that is neither gets a hand-generated <c>MemoryPackFormatter&lt;T&gt;</c>
/// instead (see <see cref="FormatterPlanner"/>/<see cref="FormatterEmitter"/>), registered
/// via a <c>[ModuleInitializer]</c> so it's live before any <c>RegisterAll</c> call runs. A
/// field shape that can't be safely auto-handled reports <c>WYRD006</c> instead of silently
/// excluding the component.
///
/// Discriminators default to each type's fully qualified name, so two same-named components
/// in different namespaces don't collide - overridden by <c>[StableName]</c>. Each
/// <c>[RenamedFrom]</c> on a type emits a matching <c>RegisterAlias</c> call.
///
/// <see cref="TryExtract"/> pulls only the fully-qualified type name out of the semantic
/// model immediately, rather than carrying <see cref="INamedTypeSymbol"/> through
/// <c>Collect()</c>: symbols don't compare structurally equal across compilations, so
/// keeping one in the pipeline defeats incremental caching for the whole file.
/// <see cref="FormatterPlanner"/> operates on symbols too, but entirely within one
/// <see cref="TryExtract"/> call, never carried across that same caching boundary.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class MemoryPackRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is StructDeclarationSyntax,
                transform: static (ctx, _) => TryExtract((StructDeclarationSyntax)ctx.Node, ctx.SemanticModel))
            .Where(static result => result.Info is not null || !result.Diagnostics.IsEmpty)
            .WithTrackingName("RegisteredComponentInfo");

        context.RegisterSourceOutput(candidates.Collect(), static (spc, results) =>
        {
            foreach (var result in results)
                foreach (var diagnostic in result.Diagnostics)
                    spc.ReportDiagnostic(diagnostic);

            var infos = results.Where(r => r.Info is not null).Select(r => r.Info!.Value).ToImmutableArray();
            spc.AddSource("MemoryPackAutoRegistration.g.cs", Render(infos));
        });
    }

    private static ExtractResult TryExtract(StructDeclarationSyntax declaration, SemanticModel semanticModel)
    {
        var none = new ExtractResult(null, ImmutableArray<Diagnostic>.Empty);
        if (semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol) return none;
        if (!IsComponent(symbol)) return none;
        if (HasPersistenceIgnoreAttribute(symbol)) return none;

        var stableName = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == "Wyrd.Ecs.StableNameAttribute")
            ?.ConstructorArguments[0].Value as string;
        var discriminator = stableName ?? symbol.ToDisplayString();

        var renamedFrom = symbol.GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == "Wyrd.Ecs.RenamedFromAttribute")
            .Select(a => (string)a.ConstructorArguments[0].Value!)
            .ToImmutableArray();

        if (HasMemoryPackableAttribute(symbol) || symbol.IsUnmanagedType)
        {
            var info = new RegisteredComponentInfo(symbol.ToDisplayString(), discriminator, renamedFrom, ImmutableArray<PlannedFormatter>.Empty);
            return new ExtractResult(info, ImmutableArray<Diagnostic>.Empty);
        }

        var (formatters, diagnostics) = FormatterPlanner.Plan(symbol, declaration.GetLocation());
        if (!diagnostics.IsEmpty) return new ExtractResult(null, diagnostics);

        return new ExtractResult(new RegisteredComponentInfo(symbol.ToDisplayString(), discriminator, renamedFrom, formatters), ImmutableArray<Diagnostic>.Empty);
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
            sb.AppendLine($"        registry.Register<global::{typeName}>(\"{info.Discriminator}\",");
            sb.AppendLine("            v => global::MemoryPack.MemoryPackSerializer.Serialize(v),");
            sb.AppendLine($"            bytes => global::MemoryPack.MemoryPackSerializer.Deserialize<global::{typeName}>(bytes));");
            foreach (var old in info.RenamedFrom)
                sb.AppendLine($"        registry.RegisterAlias(\"{old}\", \"{info.Discriminator}\");");
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
        sb.AppendLine("            global::Wyrd.Ecs.Persistence.Generated.TagPersistenceAutoRegistration.RegisterAll(registry);");
        sb.AppendLine("            return builder.AddBinaryPersistence(store, registry);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public global::Wyrd.Ecs.WorldBuilder AddBinaryPersistence(string path)");
        sb.AppendLine("        {");
        sb.AppendLine("            var registry = new global::Wyrd.Ecs.CodecRegistry();");
        sb.AppendLine("            MemoryPackAutoRegistration.RegisterAll(registry);");
        sb.AppendLine("            global::Wyrd.Ecs.Persistence.Generated.TagPersistenceAutoRegistration.RegisterAll(registry);");
        sb.AppendLine("            return builder.AddBinaryPersistence(path, registry);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        var allFormatters = infos.SelectMany(i => i.GeneratedFormatters).ToImmutableArray();
        FormatterEmitter.AppendFormatters(sb, allFormatters);

        return sb.ToString();
    }

    private record struct ExtractResult(RegisteredComponentInfo? Info, ImmutableArray<Diagnostic> Diagnostics);

    private record struct RegisteredComponentInfo(string FullyQualifiedName, string Discriminator, ImmutableArray<string> RenamedFrom, ImmutableArray<PlannedFormatter> GeneratedFormatters);

    private static bool IsComponent(INamedTypeSymbol symbol) =>
        symbol.AllInterfaces.Any(i => i.ToDisplayString() == "Wyrd.Ecs.IComponent");

    private static bool HasMemoryPackableAttribute(INamedTypeSymbol symbol) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "MemoryPack.MemoryPackableAttribute");

    private static bool HasPersistenceIgnoreAttribute(INamedTypeSymbol symbol) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "Wyrd.Ecs.Persistence.PersistenceIgnoreAttribute");
}
