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
}
