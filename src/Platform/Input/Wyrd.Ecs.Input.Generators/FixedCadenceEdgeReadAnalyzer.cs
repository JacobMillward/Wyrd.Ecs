using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wyrd.Ecs.Input.Generators;

/// <summary>
/// Flags a <c>.JustPressed</c>/<c>.JustReleased</c> read off an <c>ActionState</c>-typed
/// expression inside a <c>[FixedTimestep]</c> system as <c>WYRD011</c>. That pair is only
/// safe from a Variable-cadence reader (see <c>ActionState</c>'s own doc comment); a
/// Fixed-cadence reader can miss or double-count the edge. Points the fix at
/// <c>TickJustPressed</c>/<c>TickJustReleased</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixedCadenceEdgeReadAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor UnsafeEdgeRead = new(
        id: "WYRD011",
        title: "Edge-triggered ActionState field read from a [FixedTimestep] system",
        messageFormat: "'{0}' is only safe to read from a Variable-cadence system. A [FixedTimestep] system can miss or double-count it; use '{1}' instead.",
        category: "Wyrd.Ecs.Input",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [UnsafeEdgeRead];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var access = (MemberAccessExpressionSyntax)context.Node;
        var memberName = access.Name.Identifier.ValueText;
        var safeReplacement = memberName switch
        {
            "JustPressed" => "TickJustPressed",
            "JustReleased" => "TickJustReleased",
            _ => null,
        };
        if (safeReplacement is null) return;

        if (context.SemanticModel.GetSymbolInfo(access, context.CancellationToken).Symbol
            is not IPropertySymbol { ContainingType: { Name: "ActionState" } containingType }) return;
        if (containingType.ContainingNamespace.ToDisplayString() != "Wyrd.Ecs.Input") return;

        var enclosingType = access.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (enclosingType is null) return;
        if (context.SemanticModel.GetDeclaredSymbol(enclosingType, context.CancellationToken) is not INamedTypeSymbol typeSymbol) return;

        var hasFixedTimestep = typeSymbol.GetAttributes().Any(a =>
            a.AttributeClass is { Name: "FixedTimestepAttribute" } attrClass &&
            attrClass.ContainingNamespace.ToDisplayString() == "Wyrd.Ecs");
        if (!hasFixedTimestep) return;

        context.ReportDiagnostic(Diagnostic.Create(UnsafeEdgeRead, access.GetLocation(), memberName, safeReplacement));
    }
}
