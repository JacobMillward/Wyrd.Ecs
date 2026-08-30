namespace Wyrd.Ecs.Generators.Tests;

public class EcsSystemResourceGlueTests
{
    private const string Harness = """
        using System;
        using Wyrd.Ecs;

        public struct Score : IResource { public int Value; }

        public sealed partial class ScoreReaderSystem : EcsSystem
        {
            [Resource] public partial Score Score { get; set; }
            public int Observed;
            protected override void Execute(World world, Time time) => Observed = Score.Value;
        }

        public sealed partial class ScoreWriterSystem : EcsSystem
        {
            [Resource] public partial Score Score { get; set; }
            protected override void Execute(World world, Time time) => Score = new Score { Value = 9 };
        }

        public static class Harness
        {
            public static int ReadLive()
            {
                var world = new WorldBuilder().AddResource(new Score { Value = 5 }).Build();
                world.GetResourceRef<Score>().Value = 7;
                var system = new ScoreReaderSystem();
                world.RunOnce(system, TimeSpan.Zero);
                return system.Observed;
            }

            public static int WriteForwardsToWorld()
            {
                var world = new WorldBuilder().AddResource(new Score { Value = 0 }).Build();
                world.RunOnce(new ScoreWriterSystem(), TimeSpan.Zero);
                return world.GetResource<Score>().Value;
            }
        }
        """;

    [Fact]
    public void ResourcePartialProperty_ReadsLiveFromWorld()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (int)assembly.GetType("Harness")!.GetMethod("ReadLive")!.Invoke(null, null)!;

        result.Should().Be(7, "the property should read the current World value, not a value snapshotted at construction");
    }

    [Fact]
    public void ResourcePartialProperty_WriteForwardsToWorld()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (int)assembly.GetType("Harness")!.GetMethod("WriteForwardsToWorld")!.Invoke(null, null)!;

        result.Should().Be(9, "assigning the property should write through to the World resource, not a local copy");
    }
}
