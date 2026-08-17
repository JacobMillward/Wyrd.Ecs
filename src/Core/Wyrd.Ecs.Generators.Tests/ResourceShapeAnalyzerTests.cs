using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Wyrd.Ecs.Generators.Diagnostics;

namespace Wyrd.Ecs.Generators.Tests;

public class ResourceShapeAnalyzerTests
{
    private static ImmutableArray<Diagnostic> RunAnalyzer(string source)
    {
        var compilation = GeneratorTestHost.Compile(source);
        return compilation
            .WithAnalyzers([new ResourceShapeAnalyzer()])
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void ResourcePropertyOfNonResourceType_ReportsWYRD006()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct NotAResource { public int Value; }

            public sealed partial class BadSystem : QuerySystem
            {
                [Resource] public NotAResource Data { get; private set; }
                protected override IQuery DefineQuery(Query query) => query;
                public void Update(Time time) { }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD006");
    }

    [Fact]
    public void ResourcePropertyOnPlainEcsSystem_ReportsWYRD007()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Score : IResource { public int Value; }

            public sealed class BadSystem : EcsSystem
            {
                [Resource] public Score Score { get; private set; }
                protected override void Execute(World world, Time time) { }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD007");
    }
}
