namespace Wyrd.Ecs.Generators.Tests;

public class QueryChainGeneratorParallelForEachTests
{
    private const string Harness = """
        using System.Threading;
        using Wyrd.Ecs;

        public struct Position : IComponent { public float X; }

        public static class Harness
        {
            public static int Run()
            {
                var world = new World();
                for (var i = 0; i < 200; i++)
                    world.Commands.CreateEntity(new Position { X = i });
                world.ApplyCommands();

                var visited = 0;
                world.Query().With<Writes<Position>>()
                    .ParallelForEach(0, (in int _, ref Position p) =>
                    {
                        p.X += 1f;
                        Interlocked.Increment(ref visited);
                    });

                return visited;
            }

            public static int RunNoUniform()
            {
                var world = new World();
                for (var i = 0; i < 200; i++)
                    world.Commands.CreateEntity(new Position { X = i });
                world.ApplyCommands();

                var visited = 0;
                world.Query().With<Writes<Position>>()
                    .ParallelForEach((ref Position p) =>
                    {
                        p.X += 1f;
                        Interlocked.Increment(ref visited);
                    });

                return visited;
            }
        }
        """;

    [Fact]
    public void ParallelForEach_VisitsEveryMatchingEntity()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (int)assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        result.Should().Be(200);
    }

    [Fact]
    public void ParallelForEach_NoUniformOverload_VisitsEveryMatchingEntity()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (int)assembly.GetType("Harness")!.GetMethod("RunNoUniform")!.Invoke(null, null)!;

        result.Should().Be(200);
    }
}
