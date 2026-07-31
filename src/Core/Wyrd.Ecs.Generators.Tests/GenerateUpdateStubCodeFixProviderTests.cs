using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Wyrd.Ecs.Generators.Diagnostics;

namespace Wyrd.Ecs.Generators.Tests;

public class GenerateUpdateStubCodeFixProviderTests
{
    [Fact]
    public async Task MissingUpdate_GeneratesFourVariantStubs()
    {
        const string source = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public sealed class BrokenSystem : QuerySystem
            {
                protected override IQuery DefineQuery(World world) => world.Query().With<Position>();
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

        var registeredActions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            relocatedDiagnostic,
            (action, _) => registeredActions.Add(action),
            CancellationToken.None);

        await new GenerateUpdateStubCodeFixProvider().RegisterCodeFixesAsync(context);

        registeredActions.Should().HaveCount(4);

        async Task<string> ApplyAsync(CodeAction action)
        {
            var operations = await action.GetOperationsAsync(CancellationToken.None);
            var applyOperation = operations.OfType<ApplyChangesOperation>().Should().ContainSingle().Subject;
            var changedDocument = applyOperation.ChangedSolution.GetDocument(document.Id)!;
            return (await changedDocument.GetTextAsync()).ToString();
        }

        (await ApplyAsync(registeredActions.Single(a => a.EquivalenceKey == "GenerateUpdateStub")))
            .Should().Contain("public void Update(Time time, ref Position p0)");
        (await ApplyAsync(registeredActions.Single(a => a.EquivalenceKey == "GenerateUpdateStubWithWorld")))
            .Should().Contain("public void Update(Time time, World world, ref Position p0)");
        (await ApplyAsync(registeredActions.Single(a => a.EquivalenceKey == "GenerateUpdateStubWithEntityView")))
            .Should().Contain("public void Update(Time time, EntityView entity, ref Position p0)");
        (await ApplyAsync(registeredActions.Single(a => a.EquivalenceKey == "GenerateUpdateStubWithWorldAndEntityView")))
            .Should().Contain("public void Update(Time time, World world, EntityView entity, ref Position p0)");
    }
}
