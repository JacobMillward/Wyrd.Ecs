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
            protected override IQuery DefineQuery(Query query) =>
                query.With<Position>().With<Velocity>();

            public void Update(Time time, ref Position p, in Velocity v) => p.X += v.X;
        }

        public static class Harness
        {
            public static float Run()
            {
                var world = new World();
                world.Commands.CreateEntity(new Position { X = 1f }, new Velocity { X = 2f });
                world.Commands.CreateEntity(new Position { X = 10f }, new Velocity { X = 20f });
                world.ApplyCommands();

                world.RunOnce(new MovementSystem(), TimeSpan.Zero);

                var total = 0f;
                world.Query().With<Position>()
                    .ForEach(0, (in int _, in Position p) => total += p.X);
                return total;
            }

            public static bool RegistersSystemRegistryAccess() =>
                SystemRegistry.Access.TryGetValue(typeof(MovementSystem), out var access)
                && access.Writes.Count == 1 && access.Writes[0] == typeof(Position)
                && access.Reads.Count == 1 && access.Reads[0] == typeof(Velocity);
        }
        """;

    [Fact]
    public void DefineQueryAndUpdate_RunOnce_MutatesThroughToRealStorage()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (float)assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        result.Should().Be(33f, "Entity 1: 1 + 2 = 3, Entity 2: 10 + 20 = 30, sum: 33");
    }

    [Fact]
    public void QuerySystem_RegistersSystemRegistryAccessTheSameAsAnAdHocChainWould()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (bool)assembly.GetType("Harness")!.GetMethod("RegistersSystemRegistryAccess")!.Invoke(null, null)!;

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
            protected override IQuery DefineQuery(Query query) =>
                query.With<Position>().With<Velocity>().With<Health>();

            public void Update(Time time, ref Position p, in Velocity v, in Health h) => p.X += v.X + h.Current;
        }

        public static class Harness
        {
            public static float Run()
            {
                var world = new World();
                world.Commands.CreateEntity(new Position { X = 1f }, new Velocity { X = 2f }, new Health { Current = 3f });
                world.ApplyCommands();

                world.RunOnce(new ThreeComponentSystem(), TimeSpan.Zero);

                var total = 0f;
                world.Query().With<Position>().ForEach(0, (in int _, in Position p) => total += p.X);
                return total;
            }
        }
        """;

    [Fact]
    public void DefineQuery_DoesNotNeedItsOwnSignatureUpdatedWhenTheChainGrows()
    {
        // IQuery means adding a component to the chain (Health here) only ever touches
        // DefineQuery's body, never its declared return type.
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(EditedShapeHarness));

        var result = (float)assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        result.Should().Be(6f, "1 (Position.X) + 2 (Velocity.X) + 3 (Health.Current)");
    }

    [Fact]
    public void UpdateWithABareUnmodifiedParameter_IsNotRecognizedAsAValidQuerySystem()
    {
        var source = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public sealed class BrokenSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Position>();

                public void Update(Time time, Position p) { }
            }
            """;

        var result = GeneratorTestHost.Run(new QueryChainGenerator(), GeneratorTestHost.Compile(source));

        result.GeneratedTrees.Should().NotContain(t => t.FilePath.Contains("BrokenSystem"));
    }

    [Fact]
    public void FilterOnlyDefineQuery_MissingUpdate_IsNotRecognizedAsAValidQuerySystem()
    {
        var source = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public sealed class BrokenSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.Has<Position>();
            }
            """;

        var result = GeneratorTestHost.Run(new QueryChainGenerator(), GeneratorTestHost.Compile(source));

        result.GeneratedTrees.Should().NotContain(t => t.FilePath.Contains("BrokenSystem"));
    }

    [Fact]
    public void FilterOnlyDefineQuery_UpdateWithExtraParameter_IsNotRecognizedAsAValidQuerySystem()
    {
        var source = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public sealed class BrokenSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.Has<Position>();

                public void Update(Time time, ref Position p) { }
            }
            """;

        var result = GeneratorTestHost.Run(new QueryChainGenerator(), GeneratorTestHost.Compile(source));

        result.GeneratedTrees.Should().NotContain(t => t.FilePath.Contains("BrokenSystem"));
    }

    private const string WithoutFilterHarness = """
        using System;
        using Wyrd.Ecs;

        public struct Position : IComponent { public float X; }
        public struct Dead : ITag;

        public sealed partial class MoveSystem : QuerySystem
        {
            protected override IQuery DefineQuery(Query query) =>
                query.With<Position>().Without<Dead>();

            public void Update(Time time, ref Position p) => p.X += 1f;
        }

        public static class Harness
        {
            public static float Run()
            {
                var world = new World();
                Entity alive = world.Commands.CreateEntity(new Position { X = 1f });
                Entity dead = world.Commands.CreateEntity(new Position { X = 100f });
                world.Commands.AddTag<Dead>(dead);
                world.ApplyCommands();

                world.RunOnce(new MoveSystem(), TimeSpan.Zero);

                // Read back directly (not via another .ForEach over Query<(Position, Nil)>):
                // that would be a second, independent read-only call site sharing MoveSystem's
                // exact shape while wanting `in` instead of `ref` for Position, which is exactly
                // the WYRD003 conflict (see QueryChainGenerator.DeduplicateShapes). GetComponent
                // sidesteps it entirely, and is what this test actually needs: a direct value
                // check, not another generated query terminal.
                return world.GetComponent<Position>(alive).X + world.GetComponent<Position>(dead).X;
            }
        }
        """;

    [Fact]
    public void QuerySystemWithWithoutInDefineQuery_FilterIsAppliedAtRuntime()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(WithoutFilterHarness));

        var result = (float)assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        result.Should().Be(102f, "alive: 1f, +1 from MoveSystem's own Update = 2f; dead: untouched, since Without<Dead> excludes it from MoveSystem's query, so it stays 100f; 2 + 100 = 102");
    }

    private const string SharedShapeDifferentAccessHarness = """
        using System;
        using Wyrd.Ecs;

        public struct Score : IComponent { public int Value; }

        public sealed partial class WriterSystem : QuerySystem
        {
            protected override IQuery DefineQuery(Query query) => query.With<Score>();

            public void Update(Time time, ref Score score) => score.Value += 1;
        }

        public sealed partial class ReaderSystem : QuerySystem
        {
            public static int LastSeen;

            protected override IQuery DefineQuery(Query query) => query.With<Score>();

            public void Update(Time time, in Score score) => LastSeen = score.Value;
        }

        public static class Harness
        {
            public static int Run()
            {
                var world = new World();
                world.Commands.CreateEntity(new Score { Value = 5 });
                world.ApplyCommands();

                world.RunOnce(new WriterSystem(), TimeSpan.Zero);
                world.RunOnce(new ReaderSystem(), TimeSpan.Zero);

                return ReaderSystem.LastSeen;
            }
        }
        """;

    [Fact]
    public void TwoQuerySystems_SameShape_DifferentAccess_BothCompileAndRun()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(SharedShapeDifferentAccessHarness));

        var result = (int)assembly.GetType("Harness")!.GetMethod("Run")!.Invoke(null, null)!;

        result.Should().Be(6, "WriterSystem's ref Score increments 5 to 6 before ReaderSystem's in Score reads it -- same query shape, different access, no WYRD003 collision");
    }
}
