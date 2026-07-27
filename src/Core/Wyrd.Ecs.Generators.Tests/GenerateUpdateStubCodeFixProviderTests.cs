using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Wyrd.Ecs.Generators.Diagnostics;

namespace Wyrd.Ecs.Generators.Tests;

public class GenerateUpdateStubCodeFixProviderTests
{
    [Fact]
    public async Task MissingUpdate_GeneratesAMatchingStub()
    {
        const string source = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            public struct Velocity : IComponent { public float X; }

            public sealed class BrokenSystem : QuerySystem
            {
                protected override IQuery DefineQuery(World world) => world.Query().With<Position>().With<Velocity>();
            }
            """;

        var compilation = GeneratorTestHost.Compile(source);
        var analyzerDiagnostics = await compilation
            .WithAnalyzers([new QuerySystemShapeAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
        var wyrd002 = analyzerDiagnostics.Should().ContainSingle(d => d.Id == "WYRD002").Subject;

        var document = GeneratorTestHost.CreateDocument(source);
        // The diagnostic was computed against GeneratorTestHost.Compile's standalone
        // CSharpCompilation; re-locate it against the workspace document's own tree (same
        // source text, so the same span) since Diagnostic.Location ties to a specific
        // SyntaxTree instance, not just a span.
        var documentTree = (await document.GetSyntaxTreeAsync())!;
        var relocatedDiagnostic = Diagnostic.Create(
            wyrd002.Descriptor, Location.Create(documentTree, wyrd002.Location.SourceSpan));

        CodeAction? registeredAction = null;
        var context = new CodeFixContext(
            document,
            relocatedDiagnostic,
            (action, _) => registeredAction ??= action,
            CancellationToken.None);

        await new GenerateUpdateStubCodeFixProvider().RegisterCodeFixesAsync(context);

        registeredAction.Should().NotBeNull();
        var operations = await registeredAction!.GetOperationsAsync(CancellationToken.None);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Should().ContainSingle().Subject;

        var changedDocument = applyOperation.ChangedSolution.GetDocument(document.Id)!;
        var changedText = (await changedDocument.GetTextAsync()).ToString();

        changedText.Should().Contain("public void Update(Time time, ref Position p0, ref Velocity p1)");
    }
}
