using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Wyrd.Ecs.Generators.Diagnostics;

namespace Wyrd.Ecs.Generators.Tests;

public class StaleResourceSnapshotAnalyzerTests
{
    private static ImmutableArray<Diagnostic> RunAnalyzer(string source)
    {
        var compilation = GeneratorTestHost.Compile(source);
        return compilation
            .WithAnalyzers([new StaleResourceSnapshotAnalyzer()])
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void ConstructorInjectedResource_StoredInAField_ReportsWYRD008()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Score : IResource { public int Value; }

            public sealed class BadSystem : EcsSystem
            {
                private Score _cached;
                public BadSystem(World world, in Score score) => _cached = score;
                protected override void Execute(World world, Time time) { }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD008");
    }

    [Fact]
    public void ResourcePropertyAssignedInConstructor_ReportsWYRD008()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Score : IResource { public int Value; }

            public sealed partial class BadSystem : QuerySystem
            {
                [Resource] public Score Score { get; set; }
                public BadSystem() => Score = new Score { Value = 1 };
                protected override IQuery DefineQuery(Query query) => query;
                public void Update(Time time) { }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD008");
    }

    [Fact]
    public void ResourcePropertyValue_StoredInAnotherField_ReportsWYRD008()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Score : IResource { public int Value; }

            public sealed partial class BadSystem : QuerySystem
            {
                [Resource] public Score Score { get; private set; }
                private Score _cached;
                protected override IQuery DefineQuery(Query query) => query;
                public void Update(Time time) => _cached = Score;
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD008");
    }

    [Fact]
    public void UsingAResourcePropertyDirectly_WithoutStoringIt_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Score : IResource { public int Value; }

            public sealed partial class GoodSystem : QuerySystem
            {
                [Resource] public Score Score { get; private set; }
                protected override IQuery DefineQuery(Query query) => query;
                public void Update(Time time) { var x = Score.Value; }
            }
            """);

        diagnostics.Should().BeEmpty();
    }
}
