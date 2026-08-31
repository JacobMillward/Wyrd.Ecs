using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wyrd.Ecs.Generators.Diagnostics;

/// <summary>
/// Flags a `QuerySystem` subclass whose `Update` doesn't match `DefineQuery`'s declared
/// components (missing, wrong count, wrong type, or wrong order) as `WYRD002`. A missing
/// `DefineQuery` itself needs no diagnostic: it's a real `protected abstract` member, so
/// omitting it is the ordinary `CS0534`, not this analyzer's job.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QuerySystemShapeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [WyrdDiagnostics.UpdateShapeMismatch];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.BaseType is not { Name: "QuerySystem" } baseType) return;
        if (baseType.ContainingNamespace?.ToDisplayString() != "Wyrd.Ecs") return;

        var defineQueryOnBase = baseType.GetMembers("DefineQuery").OfType<IMethodSymbol>().FirstOrDefault();
        if (defineQueryOnBase is null) return;

        var defineQuery = type.GetMembers("DefineQuery").OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsOverride && SymbolEqualityComparer.Default.Equals(m.OverriddenMethod?.OriginalDefinition, defineQueryOnBase));
        if (defineQuery is null) return; // missing entirely: CS0534, not WYRD002

        if (defineQuery.DeclaringSyntaxReferences is not [var defineQuerySyntaxRef, ..]) return;
        if (defineQuerySyntaxRef.GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax { ExpressionBody.Expression: var returnExpr }) return;

        var defineQuerySemanticModel = context.Compilation.GetSemanticModel(defineQuerySyntaxRef.SyntaxTree);
        if (defineQuerySemanticModel.GetTypeInfo(returnExpr, context.CancellationToken).Type is not INamedTypeSymbol returnType) return;

        var shape = ChainWalker.TryExtractShapeFromQueryType(returnType, context.CancellationToken, out _); // access mode comes from Update's own parameters, not from With/WithMut
        if (shape is null) return;

        var declaredComponents = shape.PendingDataElements; // already declaration order; see ChainWalker.TryExtractShapeFromQueryType
        var update = type.GetMembers("Update").OfType<IMethodSymbol>().FirstOrDefault(m => !m.IsStatic);
        var classification = update is null ? QuerySystemUpdateShape.Invalid : QuerySystemUpdateShape.Classify(update.Parameters);

        bool mismatch;
        if (update is null || !classification.IsValid)
        {
            mismatch = true;
        }
        else
        {
            var componentParameters = update.Parameters.Skip(classification.ComponentStartIndex).ToImmutableArray();
            mismatch = componentParameters.Length != declaredComponents.Length
                || componentParameters.Select((p, i) => p.Type.ToDisplayString() != declaredComponents[i]).Any(different => different);
        }
        if (!mismatch) return;

        var expected = string.Join(", ", declaredComponents);
        var location = update?.DeclaringSyntaxReferences is [var updateSyntaxRef, ..]
            ? updateSyntaxRef.GetSyntax(context.CancellationToken).GetLocation()
            : type.Locations.FirstOrDefault() ?? Location.None;

        context.ReportDiagnostic(Diagnostic.Create(
            WyrdDiagnostics.UpdateShapeMismatch, location, type.Name, declaredComponents.Length, expected));
    }
}
