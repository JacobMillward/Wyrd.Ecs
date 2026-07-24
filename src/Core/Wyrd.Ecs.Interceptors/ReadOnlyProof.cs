using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Interceptors;

/// <summary>
/// Decides whether a <c>QueryRow&lt;...&gt;.Get&lt;T&gt;()</c> call site's result is
/// ever written through. Flow-insensitive on purpose: any syntactic write target
/// found anywhere in the relevant scope disqualifies the call site, whether or not
/// real control flow could actually reach it. A missed proof just leaves that call
/// site marking dirty on every access, same as today, so this can only be
/// conservative, never wrong.
/// </summary>
internal static class ReadOnlyProof
{
    internal static bool IsProvablyReadOnly(InvocationExpressionSyntax getCall, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        var outermost = OutermostMemberAccess(getCall);

        if (outermost.Parent is ArgumentSyntax { RefKindKeyword: var refKind } argument && !refKind.IsKind(SyntaxKind.None))
        {
            if (refKind.IsKind(SyntaxKind.InKeyword)) return true;
            return IsSafeRefOutArgument(argument, semanticModel, cancellationToken);
        }

        if (outermost.Parent is RefExpressionSyntax { Parent: EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator } } && declarator.Parent is VariableDeclarationSyntax { Type: RefTypeSyntax })
        {
            var symbol = semanticModel.GetDeclaredSymbol(declarator, cancellationToken);
            if (symbol is null) return false;
            var enclosingBlock = declarator.FirstAncestorOrSelf<BlockSyntax>();
            if (enclosingBlock is null) return false;
            return !AnyWriteToSymbol(enclosingBlock, symbol, semanticModel, cancellationToken);
        }

        if (IsWriteTarget(outermost)) return false;

        // A bare `row.Get<T>().X` (no ref local, no argument, not itself a write
        // target) is a one-shot read: nothing to trace further.
        return true;
    }

    private static bool IsSafeRefOutArgument(ArgumentSyntax argument, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (argument.Parent?.Parent is not InvocationExpressionSyntax callSite) return false;
        if (semanticModel.GetSymbolInfo(callSite, cancellationToken).Symbol is not IMethodSymbol callee) return false;

        // Opacity boundary: interface dispatch, virtual/abstract/override, or a
        // method this compilation doesn't have source for (a different assembly).
        if (callee.ContainingType.TypeKind == TypeKind.Interface) return false;
        if (callee.IsVirtual || callee.IsAbstract || callee.IsOverride) return false;
        if (callee.DeclaringSyntaxReferences.Length == 0) return false;

        var parameterIndex = ((ArgumentListSyntax)argument.Parent).Arguments.IndexOf(argument);
        if (parameterIndex < 0 || parameterIndex >= callee.Parameters.Length) return false;
        var parameter = callee.Parameters[parameterIndex];

        var declaration = callee.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken);
        var body = declaration switch
        {
            MethodDeclarationSyntax { Body: { } block } => (SyntaxNode)block,
            MethodDeclarationSyntax { ExpressionBody: { } arrow } => arrow,
            _ => null,
        };
        if (body is null) return false;

        var calleeModel = semanticModel.Compilation.GetSemanticModel(body.SyntaxTree);
        return !AnyWriteToSymbol(body, parameter, calleeModel, cancellationToken);
    }

    private static bool AnyWriteToSymbol(SyntaxNode scope, ISymbol symbol, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        foreach (var identifier in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identifierSymbol = semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol;
            if (!SymbolEqualityComparer.Default.Equals(identifierSymbol, symbol)) continue;

            if (IsWriteTarget(OutermostMemberAccess(identifier))) return true;
        }

        return false;
    }

    private static ExpressionSyntax OutermostMemberAccess(ExpressionSyntax expression)
    {
        while (expression.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Expression == expression)
            expression = memberAccess;
        return expression;
    }

    private static bool IsWriteTarget(ExpressionSyntax expression) =>
        expression.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == expression,
            PostfixUnaryExpressionSyntax postfix => postfix.Operand == expression,
            PrefixUnaryExpressionSyntax prefix => prefix.Operand == expression,
            ArgumentSyntax argument => argument.Expression == expression && !argument.RefKindKeyword.IsKind(SyntaxKind.None) && !argument.RefKindKeyword.IsKind(SyntaxKind.InKeyword),
            _ => false,
        };
}
