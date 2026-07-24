using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Wyrd.Ecs.SystemGenerators.Tests;

public class QuerySystemInliningGeneratorTests
{
    [Fact]
    public void GenericContainingType_EmitsNothing()
    {
        const string source = """
            using Wyrd.Ecs;

            public partial class Outer<T>
            {
                public partial class Inner : QuerySystem<Position>
                {
                    protected override void Execute(World world, ulong tick, ref Position component0) { }
                }
            }

            public struct Position : IComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new QuerySystemInliningGenerator(), GeneratorTestHost.Compile(source));

        result.Results[0].GeneratedSources.Should().BeEmpty();
    }

    [Fact]
    public void ExpressionBodiedExecute_InlinesTheExpressionAsAStatement()
    {
        const string source = """
            using Wyrd.Ecs;

            public partial class DriftSystem : QuerySystem<Position>
            {
                protected override void Execute(World world, ulong tick, ref Position component0) => component0.X += 1f;
            }

            public struct Position : IComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new QuerySystemInliningGenerator(), GeneratorTestHost.Compile(source));

        var generated = result.Results[0].GeneratedSources.Single().SourceText.ToString();
        generated.Should().Contain("protected override void OnUpdate(global::Wyrd.Ecs.World world, ulong tick)");
        generated.Should().Contain("foreach (var __row in world.Query<Position>())");
        generated.Should().Contain("ref var component0 = ref __row.Get<Position>();");
        generated.Should().Contain("component0.X += 1f;");
    }

    [Fact]
    public void PlainClass_EmitsOneHintNamedSource()
    {
        const string source = """
            using Wyrd.Ecs;

            public partial class DriftSystem : QuerySystem<Position>
            {
                protected override void Execute(World world, ulong tick, ref Position component0)
                {
                    component0.X += 1f;
                }
            }

            public struct Position : IComponent { public float X; }
            """;

        var result = GeneratorTestHost.Run(new QuerySystemInliningGenerator(), GeneratorTestHost.Compile(source));

        result.Results[0].GeneratedSources.Should().ContainSingle(s => s.HintName == "DriftSystem.OnUpdate.g.cs");
    }

    [Fact]
    public void EditingOneSystem_LeavesAnUnrelatedSystemsStepCached()
    {
        const string sourceV1 = """
            using Wyrd.Ecs;

            public partial class DriftSystem : QuerySystem<Position>
            {
                protected override void Execute(World world, ulong tick, ref Position component0)
                {
                    component0.X += 1f;
                }
            }

            public partial class UnrelatedSystem : QuerySystem<Position>
            {
                protected override void Execute(World world, ulong tick, ref Position component0)
                {
                    component0.X += 2f;
                }
            }

            public struct Position : IComponent { public float X; }
            """;

        const string sourceV2 = """
            using Wyrd.Ecs;

            public partial class DriftSystem : QuerySystem<Position>
            {
                protected override void Execute(World world, ulong tick, ref Position component0)
                {
                    component0.X += 999f;
                }
            }

            public partial class UnrelatedSystem : QuerySystem<Position>
            {
                protected override void Execute(World world, ulong tick, ref Position component0)
                {
                    component0.X += 2f;
                }
            }

            public struct Position : IComponent { public float X; }
            """;

        var generator = new QuerySystemInliningGenerator().AsSourceGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            driverOptions: new GeneratorDriverOptions(trackIncrementalGeneratorSteps: true));

        var compilationV1 = GeneratorTestHost.Compile(sourceV1);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilationV1, out _, out _);

        var originalTree = compilationV1.SyntaxTrees.Single();
        var editedTree = originalTree.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(sourceV2));
        var compilationV2 = compilationV1.ReplaceSyntaxTree(originalTree, editedTree);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilationV2, out _, out _);

        var steps = driver.GetRunResult().Results[0].TrackedSteps["GeneratedSystemInfo"];
        steps.Should().HaveCount(2);
        // Roslyn's incremental parser can preserve UnrelatedSystem's node identity across the
        // edit to DriftSystem's body (Cached: the step didn't even re-run) or re-run it and find
        // an equal-by-value result (Unchanged) - either proves the caching fix works; only
        // DriftSystem's own step, which genuinely changed, must report Modified.
        steps.Should().Contain(s => s.Outputs.Any(o =>
            o.Reason == IncrementalStepRunReason.Cached || o.Reason == IncrementalStepRunReason.Unchanged));
        steps.Should().Contain(s => s.Outputs.Any(o => o.Reason == IncrementalStepRunReason.Modified));
    }
}
