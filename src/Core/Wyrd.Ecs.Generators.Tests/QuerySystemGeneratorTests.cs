namespace Wyrd.Ecs.Generators.Tests;

public class QuerySystemGeneratorTests
{
    private const string Harness = """
        using System;
        using Wyrd.Ecs;
        using Wyrd.Ecs.Generated;

        public struct Position : IComponent { public float X; }
        public struct Velocity : IComponent { public float X; }

        public sealed partial class MovementSystem : QuerySystem
        {
            private static IQueryDefinition Build(World world) =>
                world.Query().With<Writes<Position>>().With<Reads<Velocity>>();

            private partial void Execute(Time time, ref Position p, in Velocity v) => p.X += v.X;
        }

        public static class Harness
        {
            public static float Run()
            {
                var world = new World();
                world.Commands.CreateEntity(new Position { X = 1f }, new Velocity { X = 2f });
                world.Commands.CreateEntity(new Position { X = 10f }, new Velocity { X = 20f });
                world.ApplyCommands();

                new MovementSystem().RunOnce(world, new Time(TimeSpan.Zero, TimeSpan.Zero));

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

    private const string EditedShapeHarness = """
        using System;
        using Wyrd.Ecs;
        using Wyrd.Ecs.Generated;

        public struct Position : IComponent { public float X; }
        public struct Velocity : IComponent { public float X; }
        public struct Health : IComponent { public float Current; }

        public sealed partial class ThreeComponentSystem : QuerySystem
        {
            private static IQueryDefinition Build(World world) =>
                world.Query().With<Writes<Position>>().With<Reads<Velocity>>().With<Reads<Health>>();

            private partial void Execute(Time time, ref Position p, in Velocity v, in Health h) => p.X += v.X + h.Current;
        }

        public static class Harness
        {
            public static float Run()
            {
                var world = new World();
                world.Commands.CreateEntity(new Position { X = 1f }, new Velocity { X = 2f }, new Health { Current = 3f });
                world.ApplyCommands();

                new ThreeComponentSystem().RunOnce(world, new Time(TimeSpan.Zero, TimeSpan.Zero));

                var total = 0f;
                world.Query().With<Reads<Position>>().ForEach(0, (int _, in Position p) => total += p.X);
                return total;
            }
        }
        """;

    [Fact]
    public void Build_DeclaringIQueryDefinition_DoesNotNeedItsOwnSignatureUpdatedWhenTheChainGrows()
    {
        // IQueryDefinition means adding a marker to the chain (Reads<Health> here) only
        // ever touches Build's body, never its declared return type -- this is the
        // whole point of this task.
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(EditedShapeHarness));

        var result = (float)assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        result.Should().Be(6f); // 1 (Position.X) + 2 (Velocity.X) + 3 (Health.Current)
    }
}
