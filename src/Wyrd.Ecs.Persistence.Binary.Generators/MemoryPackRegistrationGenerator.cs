using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Persistence.Binary.Generators;

/// <summary>
/// Scans for every <c>struct</c> implementing <c>Wyrd.Ecs.IComponent</c> and marked
/// <c>[MemoryPackable]</c>, and emits <c>MemoryPackAutoRegistration.RegisterAll</c>:
/// one <c>SerializerRegistry.Register&lt;T&gt;</c> call per match, using
/// <c>MemoryPackSerializer.Serialize</c>/<c>Deserialize&lt;T&gt;</c> wrapped in a
/// lambda — confirmed directly that they don't method-group-convert to
/// <c>ComponentSerializer&lt;T&gt;</c>/<c>ComponentDeserializer&lt;T&gt;</c>, a plain
/// assignment fails to compile. Only ever calls MemoryPack's public runtime API,
/// never anything MemoryPack's own generator emits by name, so there is no
/// cross-generator ordering risk here the way the JSON codec's auto-registration has.
/// Discriminators are each type's fully qualified name — unique and stable at compile
/// time, avoiding a collision between two same-named components in different
/// namespaces, which a bare simple name would risk.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class MemoryPackRegistrationGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is StructDeclarationSyntax { AttributeLists.Count: > 0 },
            transform: static (ctx, _) => ctx.SemanticModel.GetDeclaredSymbol((StructDeclarationSyntax)ctx.Node));

        context.RegisterSourceOutput(candidates.Collect(), static (spc, symbols) =>
        {
            var sb = new StringBuilder();
            sb.AppendLine("namespace Wyrd.Ecs.Persistence.Binary;");
            sb.AppendLine();
            sb.AppendLine("public static class MemoryPackAutoRegistration");
            sb.AppendLine("{");
            sb.AppendLine("    public static void RegisterAll(global::Wyrd.Ecs.SerializerRegistry registry)");
            sb.AppendLine("    {");

            foreach (var symbol in symbols)
            {
                if (symbol is not INamedTypeSymbol namedSymbol) continue;
                if (!IsComponent(namedSymbol)) continue;
                if (!HasMemoryPackableAttribute(namedSymbol)) continue;

                var typeName = namedSymbol.ToDisplayString();
                sb.AppendLine($"        registry.Register<global::{typeName}>(\"{typeName}\",");
                sb.AppendLine("            v => global::MemoryPack.MemoryPackSerializer.Serialize(v),");
                sb.AppendLine($"            bytes => global::MemoryPack.MemoryPackSerializer.Deserialize<global::{typeName}>(bytes));");
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource("MemoryPackAutoRegistration.g.cs", sb.ToString());
        });
    }

    private static bool IsComponent(INamedTypeSymbol symbol) =>
        symbol.AllInterfaces.Any(i => i.ToDisplayString() == "Wyrd.Ecs.IComponent");

    private static bool HasMemoryPackableAttribute(INamedTypeSymbol symbol) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "MemoryPack.MemoryPackableAttribute");
}
