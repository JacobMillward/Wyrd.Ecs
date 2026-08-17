namespace Wyrd.Ecs.Generators.Tests;

public class ConstructorResourceInjectionTests
{
    private const string Harness = """
        using System;
        using Wyrd.Ecs;

        public struct Score : IResource { public int Value; }

        public sealed class ReaderSystem : EcsSystem
        {
            public int Seen;
            public ReaderSystem(World world, in Score score) => Seen = score.Value;
            protected override void Execute(World world, Time time) { }
        }

        public sealed class WriterSystem : EcsSystem
        {
            public WriterSystem(World world, ref Score score) => score.Value = 42;
            protected override void Execute(World world, Time time) { }
        }

        public static class Harness
        {
            public static int Run()
            {
                var world = new WorldBuilder()
                    .AddResource(new Score { Value = 5 })
                    .AddSystem<WriterSystem>()
                    .Build();
                var reader = world.AddSystem<ReaderSystem>();
                return world.GetSystem<ReaderSystem>().Seen;
            }
        }
        """;

    [Fact]
    public void CtorWithReadResource_ResolvesTheCurrentValueAtConstruction()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (int)assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        result.Should().Be(42, "WriterSystem's ctor(World, ref Score) ran during Build(), before ReaderSystem's runtime AddSystem<T>() call");
    }
}
