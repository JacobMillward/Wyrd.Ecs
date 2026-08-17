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

    private const string WriteHarness = """
        using System;
        using Wyrd.Ecs;

        public struct Score : IResource { public int Value; }

        public sealed partial class WriterSystem : QuerySystem
        {
            [Resource] public Score Score { get; set; }
            protected override IQuery DefineQuery(Query query) => query;
            public void Update(Time time) => Score = new Score { Value = Score.Value + 1 };
        }

        public static class Harness
        {
            public static int Run()
            {
                var world = new WorldBuilder().AddResource(new Score { Value = 1 }).AddSystem<WriterSystem>().Build();
                world.Commands.CreateEntity();
                world.ApplyCommands();
                world.Update(TimeSpan.Zero);
                world.Update(TimeSpan.Zero);
                return world.GetResource<Score>().Value;
            }
        }
        """;

    [Fact]
    public void WritableResourceProperty_WritesBackAfterUpdateReturns()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(WriteHarness));

        var result = (int)assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        result.Should().Be(3, "starts at 1, WriterSystem increments it once per Update() call, two calls");
    }

    private const string AccessHarness = """
        using System.Linq;
        using Wyrd.Ecs;
        using Wyrd.Ecs.Generated;

        public struct Score : IResource { public int Value; }

        public sealed partial class ReaderSystem : QuerySystem
        {
            [Resource] public Score Score { get; private set; }
            protected override IQuery DefineQuery(Query query) => query;
            public void Update(Time time) { }
        }

        public static class Harness
        {
            public static bool ResourceIsInReads() =>
                SystemRegistry.Access.TryGetValue(typeof(ReaderSystem), out var access)
                && access.Reads.Contains(typeof(Score));
        }
        """;

    [Fact]
    public void ResourceProperty_AppearsInSystemRegistryAccessReads()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(AccessHarness));

        var result = (bool)assembly.GetType("Harness")!.GetMethod("ResourceIsInReads")!.Invoke(null, null)!;

        result.Should().BeTrue();
    }
}
