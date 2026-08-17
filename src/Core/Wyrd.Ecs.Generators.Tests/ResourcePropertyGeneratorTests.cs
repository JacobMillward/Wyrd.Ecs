namespace Wyrd.Ecs.Generators.Tests;

public class ResourcePropertyGeneratorTests
{
    private const string Harness = """
        using System;
        using Wyrd.Ecs;

        public struct Score : IResource { public int Value; }

        public sealed partial class ReaderSystem : QuerySystem
        {
            [Resource] public Score Score { get; private set; }
            protected override IQuery DefineQuery(Query query) => query;
            public void Update(Time time) { }
            public int Seen() => Score.Value;
        }

        public static class Harness
        {
            public static int Run()
            {
                var world = new WorldBuilder().AddResource(new Score { Value = 7 }).AddSystem<ReaderSystem>().Build();
                world.Update(TimeSpan.Zero);
                return world.GetSystem<ReaderSystem>().Seen();
            }
        }
        """;

    [Fact]
    public void ReadOnlyResourceProperty_IsPopulatedBeforeUpdateRuns()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (int)assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        result.Should().Be(7);
    }
}
