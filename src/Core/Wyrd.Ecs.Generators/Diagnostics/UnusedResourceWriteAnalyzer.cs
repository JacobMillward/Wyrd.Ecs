using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wyrd.Ecs.Generators.Diagnostics;

/// <summary>
/// Flags a `[Resource]` property with a public setter (declared write access, which the
/// scheduler treats as a write dependency) that no method on the containing `EcsSystem`
/// ever actually assigns to, as WYRD009. Scans every method on the type, not just `Update`,
/// so a write performed via a helper method `Update` calls still counts. Constructors are
/// excluded: assigning a `[Resource]` property there is already WYRD008's concern (it has
/// no effect, since the generator overwrites the property before every `Execute`), not a
/// legitimate write this check should credit. Applies to `[Resource]` on any `EcsSystem`,
/// not only `QuerySystem` — `[Resource]` is legal on both (see `ResourceShapeAnalyzer`).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnusedResourceWriteAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [WyrdDiagnostics.UnusedResourceWriteAccess];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!InheritsFromEcsSystem(type)) return;

        var writableResourceProperties = type.GetMembers().OfType<IPropertySymbol>()
            .Where(p => HasResourceAttribute(p) && p.SetMethod is { DeclaredAccessibility: Accessibility.Public })
            .ToList();
        if (writableResourceProperties.Count == 0) return;

        var assignedProperties = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind != MethodKind.Constructor))
        {
            foreach (var syntaxRef in method.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax(context.CancellationToken);
                var model = context.Compilation.GetSemanticModel(syntax.SyntaxTree);
                foreach (var assignment in syntax.DescendantNodesAndSelf().OfType<AssignmentExpressionSyntax>())
                {
                    if (model.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is IPropertySymbol assignedProperty)
                        assignedProperties.Add(assignedProperty);
                }
            }
        }

        foreach (var property in writableResourceProperties)
        {
            if (assignedProperties.Contains(property)) continue;
            if (property.DeclaringSyntaxReferences is not [var propertySyntaxRef, ..]) continue;
            var location = propertySyntaxRef.GetSyntax(context.CancellationToken).GetLocation();
            context.ReportDiagnostic(Diagnostic.Create(WyrdDiagnostics.UnusedResourceWriteAccess, location, property.Name, type.ToDisplayString()));
        }
    }

    private static bool HasResourceAttribute(IPropertySymbol property) =>
        property.GetAttributes().Any(a => a.AttributeClass is { Name: "ResourceAttribute", ContainingNamespace.Name: "Ecs" } ac && ac.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs");

    private static bool InheritsFromEcsSystem(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
            if (current is { Name: "EcsSystem", ContainingNamespace.Name: "Ecs" } && current.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs")
                return true;
        return false;
    }
}
