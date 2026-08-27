using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wyrd.Ecs.Generators.Tests;

public class RefKindConversionSuppressorTests
{
    [Fact]
    public async Task SuppressesCS9198_OnWyrdQueryTerminal_ButNotElsewhere()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public struct Health : IComponent { public float Current; }

            public delegate void RefAction(ref int a);

            public static class SiteA
            {
                // Genuine WYRD003-style collision: two variants of the same shape, one ref,
                // one in -- the exact case that needs the canonical-overload conversion.
                public static void M(World world) =>
                    world.Query().With<Health>().ForEach(0, (in int _, in Health h) => { });

                public static void N(World world) =>
                    world.Query().With<Health>().ForEach(0, (in int _, ref Health h) => { h.Current += 1; });

                // Unrelated ref/in mismatch, nothing to do with Wyrd's generated
                // delegates -- must NOT be suppressed.
                public static void Unrelated()
                {
                    RefAction r = (in int x) => { };
                }
            }
            """);

        var generatorResult = GeneratorTestHost.Run(new QueryChainGenerator(), compilation);
        var withGenerated = compilation.AddSyntaxTrees(
            generatorResult.Results[0].GeneratedSources.Select(s => s.SyntaxTree));

        // reportSuppressedDiagnostics: true -- otherwise a suppressed diagnostic is
        // omitted from the result entirely rather than included with IsSuppressed=true.
        var analyzerOptions = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]), onAnalyzerException: null,
            concurrentAnalysis: true, logAnalyzerExecutionTime: false, reportSuppressedDiagnostics: true);
        ImmutableArray<DiagnosticAnalyzer> analyzers = [new RefKindConversionSuppressor()];
        var compilationWithAnalyzers = withGenerated.WithAnalyzers(analyzers, analyzerOptions);

        var diagnostics = await compilationWithAnalyzers.GetAllDiagnosticsAsync();
        var cs9198 = diagnostics.Where(d => d.Id == "CS9198").ToList();

        cs9198.Should().Contain(d => d.IsSuppressed && d.Location.SourceTree!.FilePath == "File0.cs",
            "SiteA's own in-to-ref query terminal conversion should be suppressed");
        cs9198.Should().Contain(d => !d.IsSuppressed && d.Location.SourceTree!.FilePath == "File0.cs",
            "the unrelated user delegate conversion in the same file should not be");
    }
}
