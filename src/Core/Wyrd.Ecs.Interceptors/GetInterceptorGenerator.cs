using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Interceptors;

/// <summary>
/// Finds every <c>Get&lt;T&gt;()</c> call on a <c>Wyrd.Ecs.QueryRow&lt;...&gt;</c>
/// and, where <see cref="ReadOnlyProof.IsProvablyReadOnly"/> proves the result is
/// never written through, emits an interceptor that redirects the call to
/// <c>GetUnmarked&lt;T&gt;()</c> instead.
///
/// <see cref="TryExtract"/> resolves everything semantic-model-dependent (the symbol
/// match, the read-only proof, and the interceptable location's attribute syntax
/// string) immediately, rather than carrying the <see cref="InvocationExpressionSyntax"/>/
/// <see cref="SemanticModel"/> pair through <c>Collect()</c>. Two caveats this doesn't
/// fix, both left as-is because they're outside this pass's scope:
/// <list type="bullet">
/// <item>Interceptor names are assigned positionally from the collected list's order
/// (<c>Intercepted1</c>, <c>Intercepted2</c>, ...), so adding or removing one
/// intercepted call still shifts every later one's name and invalidates its cached
/// output.</item>
/// <item><see cref="SemanticModel.GetInterceptableLocation"/>'s attribute data encodes
/// a checksum of the whole containing file's content, not just the call site's
/// position, so any edit anywhere in a file changes every interceptable location's
/// hash in that same file - a candidate can only stay cached across an edit to a
/// <em>different</em> file, never its own. This is a property of that API, not
/// something a pipeline-shape change here could work around.</item>
/// </list>
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class GetInterceptorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax { Identifier.ValueText: "Get", TypeArgumentList.Arguments.Count: 1 } }
                },
                transform: static (ctx, _) => TryExtract((InvocationExpressionSyntax)ctx.Node, ctx.SemanticModel))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!.Value)
            .WithTrackingName("InterceptedGetInfo");

        context.RegisterSourceOutput(candidates.Collect(), static (spc, items) =>
            spc.AddSource("GetInterceptors.g.cs", Render(items)));
    }

    private static InterceptedGetInfo? TryExtract(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol { Name: "Get", ReturnsByRef: true } method) return null;
        if (!IsQueryRow(method.ContainingType)) return null;
        if (!ReadOnlyProof.IsProvablyReadOnly(invocation, semanticModel, default)) return null;

        var location = semanticModel.GetInterceptableLocation(invocation);
        if (location is null) return null;

        return new InterceptedGetInfo(
            InterceptsLocationAttributeSyntax: location.GetInterceptsLocationAttributeSyntax(),
            RowTypeDisplayName: method.ContainingType.ToDisplayString(),
            ComponentTypeDisplayName: method.TypeArguments[0].ToDisplayString());
    }

    private static string Render(ImmutableArray<InterceptedGetInfo> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("namespace Wyrd.Ecs.Interceptors.Generated;");
        sb.AppendLine();
        sb.AppendLine("file static class Interceptors");
        sb.AppendLine("{");

        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var index = i + 1;
            sb.AppendLine($"    {item.InterceptsLocationAttributeSyntax}");
            sb.AppendLine($"    public static ref {item.ComponentTypeDisplayName} Intercepted{index}(this in {item.RowTypeDisplayName} self) => ref self.GetUnmarked<{item.ComponentTypeDisplayName}>();");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private record struct InterceptedGetInfo(
        string InterceptsLocationAttributeSyntax,
        string RowTypeDisplayName,
        string ComponentTypeDisplayName);

    private static bool IsQueryRow(INamedTypeSymbol type)
    {
        var original = type.OriginalDefinition;
        return original.Name == "QueryRow" && original.ContainingNamespace?.ToDisplayString() == "Wyrd.Ecs";
    }
}
