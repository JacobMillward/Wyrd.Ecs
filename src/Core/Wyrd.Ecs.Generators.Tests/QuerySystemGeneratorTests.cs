namespace Wyrd.Ecs.Generators.Tests;

public class QuerySystemGeneratorTests
{
    private const string Harness = """
        using Wyrd.Ecs;
        using Wyrd.Ecs.Generated;

        public struct Position : IComponent { public float X; }
        public struct Velocity : IComponent { public float X; }

        public sealed partial class MovementSystem : QuerySystem
        {
            private static Query<(Reads<Velocity>, (Writes<Position>, Nil))> Build(World world) =>
                world.Query().With<Writes<Position>>().With<Reads<Velocity>>();

            private partial void Execute(ulong tick, ref Position p, in Velocity v) => p.X += v.X;
        }

        public static class Harness
        {
            public static float Run()
            {
                var world = new World();
                world.Commands.CreateEntity(new Position { X = 1f }, new Velocity { X = 2f });
                world.Commands.CreateEntity(new Position { X = 10f }, new Velocity { X = 20f });
                world.ApplyCommands();

                new MovementSystem().RunOnce(world, tick: 0);

                var total = 0f;
                world.Query().With<Reads<Position>>()
                    .ForEach(0, (int _, in Position p) => total += p.X);
                return total;
            }

            public static bool RegistersGeneratedSystemAccess() =>
                GeneratedSystemAccess.Entries.TryGetValue(typeof(MovementSystem), out var access)
                && access.Writes.Count == 1 && access.Writes[0] == typeof(Position)
                && access.Reads.Count == 1 && access.Reads[0] == typeof(Velocity);
        }
        """;

    [Fact]
    public void BuildAndExecute_RunOnce_MutatesThroughToRealStorage()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (float)assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        // Entity 1: 1 + 2 = 3. Entity 2: 10 + 20 = 30. Sum: 33.
        result.Should().Be(33f);
    }

    [Fact]
    public void QuerySystem_RegistersGeneratedSystemAccessTheSameAsAnAdHocChainWould()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (bool)assembly.GetType("Harness")!.GetMethod("RegistersGeneratedSystemAccess")!.Invoke(null, null)!;

        result.Should().BeTrue();
    }
}
