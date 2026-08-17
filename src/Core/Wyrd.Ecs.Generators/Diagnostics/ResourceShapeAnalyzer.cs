using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wyrd.Ecs.Generators.Diagnostics;

/// <summary>Flags a `[Resource]` property whose type isn't `IResource` (WYRD006), or that sits on a non-`QuerySystem` `EcsSystem` (WYRD007).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResourceShapeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [WyrdDiagnostics.ResourcePropertyWrongType, WyrdDiagnostics.ResourceOnNonQuerySystem];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var isQuerySystem = type.BaseType is { Name: "QuerySystem", ContainingNamespace.Name: "Ecs" } qs && qs.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs";

        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            var resourceAttribute = property.GetAttributes().FirstOrDefault(a => a.AttributeClass is { Name: "ResourceAttribute", ContainingNamespace.Name: "Ecs" } ac && ac.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs");
            if (resourceAttribute is null) continue;
            if (property.DeclaringSyntaxReferences is not [var syntaxRef, ..]) continue;
            var location = syntaxRef.GetSyntax(context.CancellationToken).GetLocation();

            if (!isQuerySystem)
            {
                context.ReportDiagnostic(Diagnostic.Create(WyrdDiagnostics.ResourceOnNonQuerySystem, location, property.Name, type.ToDisplayString()));
                continue;
            }

            var isResource = property.Type.TypeKind == TypeKind.Struct
                && property.Type.AllInterfaces.Any(i => i is { Name: "IResource", ContainingNamespace.Name: "Ecs" } && i.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs");
            if (!isResource)
                context.ReportDiagnostic(Diagnostic.Create(WyrdDiagnostics.ResourcePropertyWrongType, location, property.Name, property.Type.ToDisplayString()));
        }
    }
}
