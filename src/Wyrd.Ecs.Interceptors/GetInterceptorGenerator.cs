using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Interceptors;

/// <summary>
/// Finds every <c>Get&lt;T&gt;()</c> call on a <c>Wyrd.Ecs.QueryRow&lt;...&gt;</c>
/// and, where a later task's analysis proves the result is never written through,
/// emits an interceptor that redirects the call to <c>GetUnmarked&lt;T&gt;()</c>
/// instead. This step wires the mechanism with an always-intercept placeholder;
/// Task 3 replaces the placeholder with the real read-only proof.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class GetInterceptorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var calls = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "Get", TypeArgumentList.Arguments.Count: 1 } }
            },
            transform: static (ctx, _) => (Invocation: (InvocationExpressionSyntax)ctx.Node, ctx.SemanticModel));

        context.RegisterSourceOutput(calls.Collect(), static (spc, items) =>
        {
            var sb = new StringBuilder();
            sb.AppendLine("namespace Wyrd.Ecs.Interceptors.Generated;");
            sb.AppendLine();
            sb.AppendLine("file static class Interceptors");
            sb.AppendLine("{");

            var index = 0;
            foreach (var (invocation, semanticModel) in items)
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol { Name: "Get", ReturnsByRef: true } method) continue;
                if (!IsQueryRow(method.ContainingType)) continue;

                var location = semanticModel.GetInterceptableLocation(invocation);
                if (location is null) continue;

                index++;
                var rowType = method.ContainingType.ToDisplayString();
                var componentType = method.TypeArguments[0].ToDisplayString();
                sb.AppendLine($"    {location.GetInterceptsLocationAttributeSyntax()}");
                sb.AppendLine($"    public static ref {componentType} Intercepted{index}(this in {rowType} self) => ref self.GetUnmarked<{componentType}>();");
            }

            sb.AppendLine("}");
            spc.AddSource("GetInterceptors.g.cs", sb.ToString());
        });
    }

    private static bool IsQueryRow(INamedTypeSymbol type)
    {
        var original = type.OriginalDefinition;
        return original.Name == "QueryRow" && original.ContainingNamespace?.ToDisplayString() == "Wyrd.Ecs";
    }
}
