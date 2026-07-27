namespace Wyrd.Ecs.Generators.Tests;

public class QueryChainGeneratorPredicateForEachTests
{
    private const string Harness = """
        using Wyrd.Ecs;

        public struct Position : IComponent { public float X; }

        public static class Harness
        {
            public static int RunStoppingAtTheSecondEntity()
            {
                var world = new World();
                for (var i = 0; i < 5; i++)
                    world.Commands.CreateEntity(new Position { X = i });
                world.ApplyCommands();

                var visited = 0;
                world.Query().With<Position>()
                    .ForEach(0, (in int _, in Position p) =>
                    {
                        visited++;
                        return visited < 2; // stop after the second entity
                    });

                return visited;
            }

            public static int RunNeverStopping()
            {
                var world = new World();
                for (var i = 0; i < 5; i++)
                    world.Commands.CreateEntity(new Position { X = i });
                world.ApplyCommands();

                var visited = 0;
                world.Query().With<Position>()
                    .ForEach(0, (in int _, in Position p) =>
                    {
                        visited++;
                        return true;
                    });

                return visited;
            }

            public static int RunNoUniformStoppingAtTheSecondEntity()
            {
                var world = new World();
                for (var i = 0; i < 5; i++)
                    world.Commands.CreateEntity(new Position { X = i });
                world.ApplyCommands();

                var visited = 0;
                world.Query().With<Position>()
                    .ForEach((in Position p) =>
                    {
                        visited++;
                        return visited < 2; // stop after the second entity
                    });

                return visited;
            }
        }
        """;

    [Fact]
    public void PredicateForEach_ReturningFalse_StopsVisitingFurtherEntities()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (int)assembly.GetType("Harness")!.GetMethod("RunStoppingAtTheSecondEntity")!.Invoke(null, null)!;

        result.Should().Be(2);
    }

    [Fact]
    public void PredicateForEach_AlwaysReturningTrue_VisitsEveryMatchingEntity()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (int)assembly.GetType("Harness")!.GetMethod("RunNeverStopping")!.Invoke(null, null)!;

        result.Should().Be(5);
    }

    [Fact]
    public void PredicateForEach_NoUniformOverload_ReturningFalse_StopsVisitingFurtherEntities()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (int)assembly.GetType("Harness")!.GetMethod("RunNoUniformStoppingAtTheSecondEntity")!.Invoke(null, null)!;

        result.Should().Be(2);
    }
}
