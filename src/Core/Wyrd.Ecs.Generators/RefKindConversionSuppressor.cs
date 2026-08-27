using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wyrd.Ecs.Generators;

/// <summary>
/// Suppresses CS9198 ("reference kind modifier doesn't match") specifically for an
/// `in`-declared query-terminal lambda parameter converting to the canonical all-`ref`
/// delegate a colliding shape's `.ForEach` overload uses (see
/// <c>QueryChainGenerator.DeduplicateShapes</c>). The conversion is intentional and sound
/// there -- see <see cref="QueryChainEmitter.RenderInterceptorTarget"/>'s doc comment --
/// but the same warning firing anywhere else in a consumer's project is a real signal and
/// must not be silenced.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RefKindConversionSuppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor Rule = new(
        id: "WYRDS001",
        suppressedDiagnosticId: "CS9198",
        justification: "An 'in' query-terminal parameter intentionally converts to the shared canonical 'ref' delegate a colliding shape's overload uses; a dedicated interceptor routes it to a read-only backend.");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => ImmutableArray.Create(Rule);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Id != "CS9198") continue;
            if (!IsWyrdQueryTerminalArgument(diagnostic, context)) continue;
            context.ReportSuppression(Suppression.Create(Rule, diagnostic));
        }
    }

    private static bool IsWyrdQueryTerminalArgument(Diagnostic diagnostic, SuppressionAnalysisContext context)
    {
        var tree = diagnostic.Location.SourceTree;
        if (tree is null) return false;

        var root = tree.GetRoot(context.CancellationToken);
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        // CS9198's reported span sometimes resolves (via FindNode) to the whole argument
        // wrapping the lambda -- a parent, not an ancestor-or-self match -- rather than a
        // node strictly inside the lambda's parameter list. Check both directions.
        var lambda = node.FirstAncestorOrSelf<ParenthesizedLambdaExpressionSyntax>()
            ?? node.DescendantNodes().OfType<ParenthesizedLambdaExpressionSyntax>().FirstOrDefault();
        if (lambda is null) return false;
        if (lambda.Parent is not ArgumentSyntax argument) return false;
        if (argument.Parent?.Parent is not InvocationExpressionSyntax invocation) return false;

        // Checked against the invocation's own written syntax, not GetSymbolInfo's
        // resolved method name: once a call site is intercepted, symbol resolution here
        // reflects the interceptor (deliberately not named ForEach/ParallelForEach -- see
        // QueryChainEmitter.RenderInterceptor's doc comment), not the source text the
        // user actually wrote.
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax { Identifier.ValueText: "ForEach" or "ParallelForEach" } }) return false;

        var model = context.GetSemanticModel(tree);
        if (model.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method) return false;

        return method.ContainingType?.ContainingNamespace?.ToDisplayString() == "Wyrd.Ecs";
    }
}
