using System.Linq;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wyrd.Ecs.Generators.Diagnostics;

/// <summary>
/// Flags a data-component parameter with no `ref`/`in` modifier — in a
/// `.ForEach`/`.ParallelForEach` lambda, or in a `QuerySystem.Update` method — as
/// `WYRD001`. Runs independently of `ChainWalker`/`TryExtractQuerySystem`: those simply
/// decline to recognize a shape they can't resolve access mode for (silently, so the
/// generator doesn't emit anything for that call site) — this analyzer is what turns that
/// silence into an actionable, located error.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BareDataParameterAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [WyrdDiagnostics.BareDataParameter];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeForEachLambda, SyntaxKind.InvocationExpression);
        context.RegisterSymbolAction(AnalyzeQuerySystemUpdate, SymbolKind.NamedType);
    }

    private static void AnalyzeForEachLambda(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax
            { Name: IdentifierNameSyntax { Identifier.ValueText: "ForEach" or "ParallelForEach" }, Expression: var receiverExpr }) return;
        if (context.SemanticModel.GetTypeInfo(receiverExpr, context.CancellationToken).Type is not INamedTypeSymbol receiverType) return;
        if (!ChainWalker.IsQueryOfShape(receiverType)) return;

        if (invocation.ArgumentList.Arguments is not [.., var lastArgument]) return;
        if (lastArgument.Expression is not ParenthesizedLambdaExpressionSyntax lambda) return;

        // Every argument before the lambda is a leading uniform/state value the lambda
        // receives as its own leading parameter(s) -- see ChainWalker.TryExtractShape's
        // matching comment. Skip exactly that many of the lambda's own parameters before
        // treating the rest as data.
        var skipCount = invocation.ArgumentList.Arguments.Count - 1;
        if (lambda.ParameterList.Parameters.Count < skipCount) return;

        foreach (var parameter in lambda.ParameterList.Parameters.Skip(skipCount))
        {
            if (parameter.Modifiers.Any(SyntaxKind.RefKeyword) || parameter.Modifiers.Any(SyntaxKind.InKeyword)) continue;

            var name = parameter.Identifier.ValueText;
            context.ReportDiagnostic(Diagnostic.Create(WyrdDiagnostics.BareDataParameter, parameter.GetLocation(), name, name));
        }
    }

    private static void AnalyzeQuerySystemUpdate(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.BaseType is not { Name: "QuerySystem" } baseType) return;
        if (baseType.ContainingNamespace?.ToDisplayString() != "Wyrd.Ecs") return;

        var update = type.GetMembers("Update").OfType<IMethodSymbol>().FirstOrDefault(m => !m.IsStatic);
        if (update is null) return;

        foreach (var parameter in update.Parameters.Skip(1)) // skip the Time parameter
        {
            if (parameter.RefKind is RefKind.Ref or RefKind.In) continue;
            if (parameter.DeclaringSyntaxReferences is not [var syntaxRef, ..]) continue;

            context.ReportDiagnostic(Diagnostic.Create(WyrdDiagnostics.BareDataParameter, syntaxRef.GetSyntax(context.CancellationToken).GetLocation(), parameter.Name, parameter.Name));
        }
    }
}
