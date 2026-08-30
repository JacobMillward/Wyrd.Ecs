using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wyrd.Ecs.Generators.Diagnostics;

/// <summary>Flags a `[Resource]` property whose type isn't `IResource` (WYRD006). `[Resource]` is legal on any `EcsSystem`, not only `QuerySystem` — see `QuerySystem`'s generator-owned `Execute` vs. a plain `EcsSystem`'s generated `partial` property accessors.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResourceShapeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [WyrdDiagnostics.ResourcePropertyWrongType];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            var resourceAttribute = property.GetAttributes().FirstOrDefault(a => a.AttributeClass is { Name: "ResourceAttribute", ContainingNamespace.Name: "Ecs" } ac && ac.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs");
            if (resourceAttribute is null) continue;
            if (property.DeclaringSyntaxReferences is not [var syntaxRef, ..]) continue;
            var location = syntaxRef.GetSyntax(context.CancellationToken).GetLocation();

            var isResource = property.Type.TypeKind == TypeKind.Struct
                && property.Type.AllInterfaces.Any(i => i is { Name: "IResource", ContainingNamespace.Name: "Ecs" } && i.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs");
            if (!isResource)
                context.ReportDiagnostic(Diagnostic.Create(WyrdDiagnostics.ResourcePropertyWrongType, location, property.Name, property.Type.ToDisplayString()));
        }
    }
}
