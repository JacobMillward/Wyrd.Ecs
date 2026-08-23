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
                world.Query().With<Position>()
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
                world.Query().With<Position>()
                    .ParallelForEach((ref Position p) =>
                    {
                        p.X += 1f;
                        Interlocked.Increment(ref visited);
                    });

                return visited;
            }

            // 10_000 rows exceeds the library's parallel slice threshold (4096), so this
            // exercises sliced dispatch of one oversized archetype.
            public static double RunSliced(out int visited)
            {
                var world = new World();
                for (var i = 0; i < 10_000; i++)
                    world.Commands.CreateEntity(new Position { X = i });
                world.ApplyCommands();

                var count = 0;
                world.Query().With<Position>()
                    .ParallelForEach(0, (in int _, ref Position p) =>
                    {
                        p.X += 1f;
                        Interlocked.Increment(ref count);
                    });

                var sum = 0d;
                world.Query().With<Position>()
                    .ForEach(0, (in int _, ref Position p) => sum += p.X);

                visited = count;
                return sum;
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

    [Fact]
    public void ParallelForEach_SlicedLargeArchetype_VisitsEveryRowExactlyOnce()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var type = assembly.GetType("Harness")!;
        var arguments = new object?[1];
        var sum = (double)type.GetMethod("RunSliced")!.Invoke(null, arguments)!;
        var visited = (int)arguments[0]!;

        visited.Should().Be(10_000);
        // Seeded X values are 0..9999, one pass adds 1 to each row exactly once: the double
        // accumulation of integer-valued floats is exact, so a repeat or a skip shifts the sum.
        sum.Should().Be(50_005_000d);
    }
}
