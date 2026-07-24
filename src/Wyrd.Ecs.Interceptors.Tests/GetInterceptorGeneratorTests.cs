using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Wyrd.Ecs.Interceptors.Tests;

public class GetInterceptorGeneratorTests
{
    [Fact]
    public void PureReadThroughARowGet_EmitsAnInterceptor()
    {
        const string source = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public class Reader
            {
                public float Read(QueryRow<Position> row) => row.Get<Position>().X;
            }
            """;

        var result = GeneratorTestHost.Run(new GetInterceptorGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("namespace Wyrd.Ecs.Interceptors.Generated;");
        generated.Should().Contain("file static class Interceptors");
        generated.Should().Contain("[global::System.Runtime.CompilerServices.InterceptsLocationAttribute(");
        generated.Should().Contain("public static ref Position Intercepted1(this in Wyrd.Ecs.QueryRow<Position> self) => ref self.GetUnmarked<Position>();");
    }

    [Fact]
    public void WriteThroughARowGet_EmitsNoInterceptorForThatCall()
    {
        const string source = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public class Writer
            {
                public void Write(QueryRow<Position> row) => row.Get<Position>().X += 1f;
            }
            """;

        var result = GeneratorTestHost.Run(new GetInterceptorGenerator(), GeneratorTestHost.Compile(source));

        result.Results[0].GeneratedSources.Single().SourceText.ToString().Should().NotContain("Intercepted");
    }

    [Fact]
    public void EditingAnUnrelatedFile_LeavesTheOtherCandidateStepUnchanged()
    {
        // GetInterceptableLocation's attribute data hash covers the whole containing file's
        // content, not just the call site's position - any edit anywhere in a file changes
        // every interceptable location's hash in that same file, by design of the API, not a
        // limitation of this generator. So this test isolates the edit to a SEPARATE file to
        // prove the caching fix actually works at the granularity Roslyn allows: the candidate
        // in the untouched file must stay cached, but a same-file edit cannot (see
        // GetInterceptorGenerator's doc comment).
        const string readerSource = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public class Reader
            {
                public float Read(QueryRow<Position> row) => row.Get<Position>().X;
            }
            """;

        const string unrelatedSourceV1 = "public static class Unrelated { public static int Compute() => 1; }";
        const string unrelatedSourceV2 = "public static class Unrelated { public static int Compute() => 2; }";

        var generator = new GetInterceptorGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            parseOptions: new CSharpParseOptions(LanguageVersion.Preview),
            driverOptions: new GeneratorDriverOptions(trackIncrementalGeneratorSteps: true));

        var compilationV1 = GeneratorTestHost.Compile(readerSource, unrelatedSourceV1);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilationV1, out _, out _);

        var unrelatedTree = compilationV1.SyntaxTrees.Single(t => t.ToString() == unrelatedSourceV1);
        var editedTree = unrelatedTree.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(unrelatedSourceV2));
        var compilationV2 = compilationV1.ReplaceSyntaxTree(unrelatedTree, editedTree);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilationV2, out _, out _);

        var steps = driver.GetRunResult().Results[0].TrackedSteps["InterceptedGetInfo"];
        steps.Should().ContainSingle();
        steps[0].Outputs.Should().Contain(o =>
            o.Reason == IncrementalStepRunReason.Cached || o.Reason == IncrementalStepRunReason.Unchanged);
    }
}
