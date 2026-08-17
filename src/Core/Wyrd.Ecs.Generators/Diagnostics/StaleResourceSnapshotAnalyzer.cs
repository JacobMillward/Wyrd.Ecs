using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wyrd.Ecs.Generators.Diagnostics;

/// <summary>
/// Flags three ways a resource value's freshness guarantee gets silently broken as
/// WYRD008: a constructor-injected resource parameter stored into a field (valid only at
/// construction time), a `[Resource]` property's value stored into a different field
/// (valid only for the current tick), and a `[Resource]` property assigned to from inside
/// the constructor, which has no effect since the generator overwrites it before every
/// `Execute`.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StaleResourceSnapshotAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [WyrdDiagnostics.StaleResourceSnapshot];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
    }

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;
        var model = context.SemanticModel;

        if (model.GetSymbolInfo(assignment.Right, context.CancellationToken).Symbol is IParameterSymbol { RefKind: RefKind.None or RefKind.In } parameter
            && IsResourceType(parameter.Type)
            && model.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is IFieldSymbol
            && assignment.FirstAncestorOrSelf<ConstructorDeclarationSyntax>() is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(WyrdDiagnostics.StaleResourceSnapshot, assignment.GetLocation(),
                $"Constructor parameter '{parameter.Name}' is a resource snapshot valid only at construction time. Storing it in a field will go stale."));
            return;
        }

        if (model.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is IPropertySymbol leftProperty && HasResourceAttribute(leftProperty)
            && assignment.FirstAncestorOrSelf<ConstructorDeclarationSyntax>() is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(WyrdDiagnostics.StaleResourceSnapshot, assignment.GetLocation(),
                $"Assigning [Resource] property '{leftProperty.Name}' in the constructor has no effect: the generator overwrites it before every Execute."));
            return;
        }

        if (model.GetSymbolInfo(assignment.Right, context.CancellationToken).Symbol is IPropertySymbol rightProperty && HasResourceAttribute(rightProperty)
            && model.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is IFieldSymbol)
        {
            context.ReportDiagnostic(Diagnostic.Create(WyrdDiagnostics.StaleResourceSnapshot, assignment.GetLocation(),
                $"[Resource] property '{rightProperty.Name}' is only valid for the current tick. Storing its value in a field will go stale."));
        }
    }

    private static bool IsResourceType(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Struct && type.AllInterfaces.Any(i => i is { Name: "IResource", ContainingNamespace.Name: "Ecs" } && i.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs");

    private static bool HasResourceAttribute(IPropertySymbol property) =>
        property.GetAttributes().Any(a => a.AttributeClass is { Name: "ResourceAttribute", ContainingNamespace.Name: "Ecs" } ac && ac.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs");
}
