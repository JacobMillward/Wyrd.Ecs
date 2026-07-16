using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wyrd.Ecs.Analyzers;

/// <summary>
/// Reports WYRD001 when a <c>foreach</c> loop iterates a
/// <c>Wyrd.Ecs.MutEntityQuery&lt;T&gt;</c> without binding its loop variable with
/// <c>ref</c>. Without <c>ref</c>, <c>Current</c> (which returns <c>ref T</c>) is read
/// by value — a silent copy — and any write through the loop variable is lost instead
/// of reaching the real component.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MutEntityQueryRefForEachAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "WYRD001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "foreach over MutEntityQuery<T> must bind the loop variable with 'ref'",
        messageFormat: "foreach over 'MutEntityQuery<{0}>' must use 'foreach (ref var ... in ...)'. Without 'ref', writes to the loop variable are silently lost.",
        category: "Correctness",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeForEach, SyntaxKind.ForEachStatement);
    }

    private static void AnalyzeForEach(SyntaxNodeAnalysisContext context)
    {
        var forEach = (ForEachStatementSyntax)context.Node;

        // `foreach (ref var x in ...)` / `foreach (ref readonly var x in ...)` parse
        // the loop's Type slot as a RefTypeSyntax — both are safe, since a `ref
        // readonly` binding can't be written through at all (a separate, native
        // compiler diagnostic guards that case). Only the complete absence of `ref`
        // is the silent-copy footgun this analyzer exists to catch.
        if (forEach.Type is RefTypeSyntax) return;

        var collectionType = context.SemanticModel.GetTypeInfo(forEach.Expression, context.CancellationToken).Type;
        if (collectionType is not INamedTypeSymbol namedType) return;
        if (!IsMutEntityQuery(namedType)) return;

        var elementTypeName = namedType.TypeArguments.Length == 1
            ? namedType.TypeArguments[0].Name
            : "T";

        context.ReportDiagnostic(Diagnostic.Create(Rule, forEach.GetLocation(), elementTypeName));
    }

    private static bool IsMutEntityQuery(INamedTypeSymbol type)
    {
        var original = type.OriginalDefinition;
        return original.Arity == 1
            && original.Name == "MutEntityQuery"
            && original.ContainingNamespace?.ToDisplayString() == "Wyrd.Ecs";
    }
}
