using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wyrd.Ecs.Generators.Diagnostics;

/// <summary>
/// Flags a `world.GetResourceRef&lt;T&gt;()` call whose returned reference is read but never
/// assigned through (WYRD012) — the call-site equivalent of WYRD009 for `[Resource]`
/// properties, since every `GetResourceRef&lt;T&gt;()` call is tracked as write access to the
/// scheduler by default, regardless of whether the caller actually writes through it.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnusedResourceRefWriteAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [WyrdDiagnostics.UnusedResourceRefWriteAccess];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.Text: "GetResourceRef" } generic }) return;
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method) return;
        if (method.ContainingType is not { Name: "World", ContainingNamespace.Name: "Ecs" } worldType || worldType.ContainingNamespace.ToDisplayString() != "Wyrd.Ecs") return;
        if (method.TypeArguments is not [var resourceType]) return;

        if (IsWriteUsage(invocation, context.SemanticModel, context.CancellationToken)) return;

        context.ReportDiagnostic(Diagnostic.Create(WyrdDiagnostics.UnusedResourceRefWriteAccess, invocation.GetLocation(), resourceType.ToDisplayString()));
    }

    /// <summary>
    /// True if <paramref name="invocation"/>'s result is used as a write target: assigned
    /// directly (<c>world.GetResourceRef&lt;T&gt;() = value</c>), a member of it is assigned
    /// (<c>world.GetResourceRef&lt;T&gt;().Field = value</c>), passed by <c>ref</c>/<c>out</c>,
    /// or bound to a <c>ref</c> local (<c>ref var x = ref world.GetResourceRef&lt;T&gt;();</c>)
    /// that's written through later in the same method/constructor body — the common real
    /// pattern (see <c>PlatformSystem</c>'s constructor), not just the single-expression form.
    /// Anything else — read into a plain local, passed by value, a bare statement — is
    /// read-only usage.
    /// </summary>
    private static bool IsWriteUsage(InvocationExpressionSyntax invocation, SemanticModel semanticModel, CancellationToken ct)
    {
        switch (invocation.Parent)
        {
            case AssignmentExpressionSyntax assignment:
                return assignment.Left == invocation;
            case MemberAccessExpressionSyntax memberAccess when memberAccess.Parent is AssignmentExpressionSyntax outerAssignment:
                return outerAssignment.Left == memberAccess;
            case ArgumentSyntax argument:
                return argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword);
            case RefExpressionSyntax { Parent: EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } }:
                return IsRefLocalWrittenThrough(declarator, semanticModel, ct);
            default:
                return false;
        }
    }

    private static bool IsRefLocalWrittenThrough(VariableDeclaratorSyntax declarator, SemanticModel semanticModel, CancellationToken ct)
    {
        if (semanticModel.GetDeclaredSymbol(declarator, ct) is not ILocalSymbol local) return false;

        SyntaxNode? body = declarator.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>() switch
        {
            { Body: { } block } => block,
            { ExpressionBody: { } exprBody } => exprBody,
            _ => null,
        };
        if (body is null) return false;

        foreach (var assignment in body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            var target = assignment.Left;
            while (true)
            {
                if (target is MemberAccessExpressionSyntax memberAccess) { target = memberAccess.Expression; continue; }
                if (target is ElementAccessExpressionSyntax elementAccess) { target = elementAccess.Expression; continue; }
                break;
            }

            if (target is IdentifierNameSyntax identifier
                && SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(identifier, ct).Symbol, local))
                return true;
        }

        return false;
    }
}
