using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wyrd.Ecs.Analyzers;

/// <summary>
/// Reports WYRD001 when a local variable is declared from
/// <c>Wyrd.Ecs.QueryRow&lt;...&gt;.Get&lt;T&gt;()</c> without <c>ref</c>. Without
/// <c>ref</c>, the ref-returning call is read by value — a silent copy — and any
/// write through the local is lost instead of reaching the real component.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ForgottenRefOnGetAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "WYRD001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "A local declared from QueryRow<...>.Get<T>() must bind with 'ref'",
        messageFormat: "'{0}' is declared from 'Get<{1}>()' without 'ref'. Without 'ref', this binds a copy and any write through it is silently lost.",
        category: "Correctness",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeVariableDeclarator, SyntaxKind.VariableDeclarator);
    }

    private static void AnalyzeVariableDeclarator(SyntaxNodeAnalysisContext context)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;
        if (declarator.Initializer is not { } initializer) return;
        if (initializer.Value is not InvocationExpressionSyntax invocation) return;
        if (declarator.Parent is not VariableDeclarationSyntax declaration) return;
        if (declaration.Type is RefTypeSyntax) return;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol { Name: "Get", ReturnsByRef: true } symbol)
            return;
        if (!IsQueryRow(symbol.ContainingType)) return;

        var componentType = symbol.TypeArguments.Length == 1 ? symbol.TypeArguments[0].Name : "T";
        // Report on the whole local-declaration statement (`var x = row.Get<T>();`),
        // not just the declarator, since that's what a reader actually wants
        // highlighted.
        var location = declaration.Parent is LocalDeclarationStatementSyntax localDeclaration
            ? localDeclaration.GetLocation()
            : declaration.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, declarator.Identifier.Text, componentType));
    }

    private static bool IsQueryRow(INamedTypeSymbol type)
    {
        var original = type.OriginalDefinition;
        return original.Name == "QueryRow" && original.ContainingNamespace?.ToDisplayString() == "Wyrd.Ecs";
    }
}
