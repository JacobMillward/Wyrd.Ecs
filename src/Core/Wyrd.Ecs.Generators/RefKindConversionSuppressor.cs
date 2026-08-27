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
        // Grouped by tree so a file with several colliding call sites fetches its semantic
        // model once, not once per diagnostic.
        var byTree = context.ReportedDiagnostics
            .Where(d => d.Id == "CS9198" && d.Location.SourceTree is not null)
            .GroupBy(d => d.Location.SourceTree!);

        foreach (var group in byTree)
        {
            var model = context.GetSemanticModel(group.Key);
            var root = group.Key.GetRoot(context.CancellationToken);

            foreach (var diagnostic in group)
            {
                if (!IsWyrdQueryTerminalArgument(diagnostic, root, model, context.CancellationToken)) continue;
                context.ReportSuppression(Suppression.Create(Rule, diagnostic));
            }
        }
    }

    private static bool IsWyrdQueryTerminalArgument(Diagnostic diagnostic, SyntaxNode root, SemanticModel model, System.Threading.CancellationToken ct)
    {
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
        if (ChainWalker.TryGetInvokedMethodName(invocation) is not { } methodName) return false;
        if (!ChainWalker.IsChainTerminalMethodName(methodName)) return false;

        if (model.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol method) return false;

        return method.ContainingType?.ContainingNamespace?.ToDisplayString() == "Wyrd.Ecs";
    }
}
