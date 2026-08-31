using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace Wyrd.Ecs.Generators.Diagnostics;

/// <summary>
/// Offers "Generate Update method" on `WYRD002`'s "missing entirely" case. Inserts a
/// concrete stub with every data parameter defaulted to `ref` (the safe,
/// maximally-permissive choice), in `DefineQuery`'s declared order.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class GenerateUpdateStubCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ["WYRD002"];

    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null) return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            var classDecl = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (classDecl is null) continue;
            if (semanticModel.GetDeclaredSymbol(classDecl, context.CancellationToken) is not INamedTypeSymbol classSymbol) continue;

            // Only handle "missing entirely": if Update already exists (a count/type/order
            // mismatch instead), there's an existing method to reconcile with by hand, not a
            // stub to generate from nothing.
            if (classSymbol.GetMembers("Update").OfType<IMethodSymbol>().Any(m => !m.IsStatic)) continue;

            var baseType = classSymbol.BaseType;
            var defineQueryOnBase = baseType?.GetMembers("DefineQuery").OfType<IMethodSymbol>().FirstOrDefault();
            if (defineQueryOnBase is null) continue;
            var defineQuery = classSymbol.GetMembers("DefineQuery").OfType<IMethodSymbol>()
                .FirstOrDefault(m => m.IsOverride && SymbolEqualityComparer.Default.Equals(m.OverriddenMethod?.OriginalDefinition, defineQueryOnBase));
            if (defineQuery?.DeclaringSyntaxReferences is not [var syntaxRef, ..]) continue;
            if (syntaxRef.GetSyntax(context.CancellationToken) is not MethodDeclarationSyntax { ExpressionBody.Expression: var returnExpr }) continue;
            if (semanticModel.GetTypeInfo(returnExpr, context.CancellationToken).Type is not INamedTypeSymbol returnType) continue;

            var shape = ChainWalker.TryExtractShapeFromQueryType(returnType, context.CancellationToken, out _); // access mode comes from Update's own parameters, not from With/WithMut
            if (shape is null) continue;
            var declaredComponents = shape.PendingDataElements; // already declaration order; see ChainWalker.TryExtractShapeFromQueryType

            foreach (var (title, key, includeWorld, includeEntityView) in Variants)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title,
                        ct => GenerateStubAsync(context.Document, classDecl, declaredComponents, includeWorld, includeEntityView, ct),
                        equivalenceKey: key),
                    diagnostic);
            }
        }
    }

    /// <summary>The four valid leading-parameter combinations, canonical order (Time, World, EntityView).</summary>
    private static readonly (string Title, string Key, bool IncludeWorld, bool IncludeEntityView)[] Variants =
    [
        ("Generate Update method", "GenerateUpdateStub", false, false),
        ("Generate Update method (with World)", "GenerateUpdateStubWithWorld", true, false),
        ("Generate Update method (with EntityView)", "GenerateUpdateStubWithEntityView", false, true),
        ("Generate Update method (with World, EntityView)", "GenerateUpdateStubWithWorldAndEntityView", true, true),
    ];

    private static async Task<Document> GenerateStubAsync(
        Document document, ClassDeclarationSyntax classDecl, ImmutableArray<string> declaredComponents,
        bool includeWorld, bool includeEntityView, CancellationToken ct)
    {
        var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
        var generator = editor.Generator;

        var leading = new List<ParameterSyntax> { (ParameterSyntax)generator.ParameterDeclaration("time", generator.IdentifierName("Time")) };
        if (includeWorld) leading.Add((ParameterSyntax)generator.ParameterDeclaration("world", generator.IdentifierName("World")));
        if (includeEntityView) leading.Add((ParameterSyntax)generator.ParameterDeclaration("entity", generator.IdentifierName("EntityView")));

        var parameters = leading.Concat(declaredComponents.Select((typeName, i) =>
            ((ParameterSyntax)generator.ParameterDeclaration($"p{i}", generator.IdentifierName(typeName)))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.RefKeyword)))));

        var method = SyntaxFactory.MethodDeclaration(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "Update")
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)))
            .WithBody(SyntaxFactory.Block())
            .WithAdditionalAnnotations(Formatter.Annotation);

        editor.AddMember(classDecl, method);
        return editor.GetChangedDocument();
    }
}
