namespace Wyrd.Ecs.Generators.Tests;

public class QueryChainGeneratorForEachTests
{
    private const string Harness = """
        using Wyrd.Ecs;

        public struct Position : IComponent { public float X; }
        public struct Velocity : IComponent { public float X; }

        public static class Harness
        {
            public static float RunTwoComponent()
            {
                var world = new World();
                world.Commands.CreateEntity(new Position { X = 1f }, new Velocity { X = 2f });
                world.Commands.CreateEntity(new Position { X = 10f }, new Velocity { X = 20f });
                world.ApplyCommands();

                var total = 0f;
                world.Query().With<Writes<Position>>().With<Reads<Velocity>>()
                    .ForEach(0, (in int _, ref Position p, in Velocity v) =>
                    {
                        p.X += v.X;
                        total += p.X;
                    });

                return total;
            }

            public static int RunNoFilters()
            {
                var world = new World();
                world.Commands.CreateEntity(new Position { X = 1f });
                world.Commands.CreateEntity(new Position { X = 2f });
                world.ApplyCommands();

                var count = 0;
                world.Query().With<Writes<Position>>()
                    .ForEach(0, (in int _, ref Position p) => count++);

                return count;
            }

            public static float RunNoUniformTwoComponent()
            {
                var world = new World();
                world.Commands.CreateEntity(new Position { X = 1f }, new Velocity { X = 2f });
                world.Commands.CreateEntity(new Position { X = 10f }, new Velocity { X = 20f });
                world.ApplyCommands();

                var total = 0f;
                world.Query().With<Writes<Position>>().With<Reads<Velocity>>()
                    .ForEach((ref Position p, in Velocity v) =>
                    {
                        p.X += v.X;
                        total += p.X;
                    });

                return total;
            }

            public static int RunBothOverloadsAgainstTheSameShape()
            {
                var world = new World();
                world.Commands.CreateEntity(new Position { X = 1f });
                world.ApplyCommands();

                var visitedByUniformForm = 0;
                var visitedByPlainForm = 0;

                world.Query().With<Writes<Position>>().ForEach(0, (in int _, ref Position p) => visitedByUniformForm++);
                world.Query().With<Writes<Position>>().ForEach((ref Position p) => visitedByPlainForm++);

                return visitedByUniformForm + visitedByPlainForm;
            }
        }
        """;

    [Fact]
    public void ForEach_VisitsEveryMatchingEntity_AndMutatesThroughToRealStorage()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (float)assembly.GetType("Harness")!.GetMethod("RunTwoComponent")!.Invoke(null, null)!;

        // Entity 1: Position.X 1+2=3. Entity 2: Position.X 10+20=30. Sum of running totals: 3, then 3+30=33.
        result.Should().Be(33f);
    }

    [Fact]
    public void ForEach_SingleComponentShape_VisitsEveryMatchingEntity()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (int)assembly.GetType("Harness")!.GetMethod("RunNoFilters")!.Invoke(null, null)!;

        result.Should().Be(2);
    }

    [Fact]
    public void ForEach_NoUniformOverload_VisitsEveryMatchingEntity_AndMutatesThroughToRealStorage()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (float)assembly.GetType("Harness")!.GetMethod("RunNoUniformTwoComponent")!.Invoke(null, null)!;

        result.Should().Be(33f);
    }

    [Fact]
    public void ForEach_UniformAndNoUniformOverloads_CoexistOnTheSameShape_WithoutAmbiguity()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (int)assembly.GetType("Harness")!.GetMethod("RunBothOverloadsAgainstTheSameShape")!.Invoke(null, null)!;

        result.Should().Be(2, "one visit from the uniform-carrying overload, one from the plain overload");
    }
}
