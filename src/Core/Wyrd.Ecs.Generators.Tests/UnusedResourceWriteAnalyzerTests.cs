using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Wyrd.Ecs.Generators.Diagnostics;

namespace Wyrd.Ecs.Generators.Tests;

public class UnusedResourceWriteAnalyzerTests
{
    private static ImmutableArray<Diagnostic> RunAnalyzer(string source)
    {
        var compilation = GeneratorTestHost.Compile(source);
        return compilation
            .WithAnalyzers([new UnusedResourceWriteAnalyzer()])
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void PublicSetterNeverAssignedInUpdate_ReportsWYRD009()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Score : IResource { public int Value; }

            public sealed partial class ReaderOnlySystem : QuerySystem
            {
                [Resource] public Score Score { get; set; }
                protected override IQuery DefineQuery(Query query) => query;
                public void Update(Time time) { var x = Score.Value; }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD009");
    }

    [Fact]
    public void PublicSetterAssignedInUpdate_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Score : IResource { public int Value; }

            public sealed partial class WriterSystem : QuerySystem
            {
                [Resource] public Score Score { get; set; }
                protected override IQuery DefineQuery(Query query) => query;
                public void Update(Time time) => Score = new Score { Value = Score.Value + 1 };
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void PublicSetterAssignedInAHelperMethodCalledFromUpdate_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Score : IResource { public int Value; }

            public sealed partial class WriterSystem : QuerySystem
            {
                [Resource] public Score Score { get; set; }
                protected override IQuery DefineQuery(Query query) => query;
                public void Update(Time time) => Increment();
                private void Increment() => Score = new Score { Value = Score.Value + 1 };
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ReadOnlyResourceProperty_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Score : IResource { public int Value; }

            public sealed partial class ReaderSystem : QuerySystem
            {
                [Resource] public Score Score { get; private set; }
                protected override IQuery DefineQuery(Query query) => query;
                public void Update(Time time) { var x = Score.Value; }
            }
            """);

        diagnostics.Should().BeEmpty();
    }
}
