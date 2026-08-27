using System.Reflection;

namespace Wyrd.Ecs.Generators.Tests;

public class QueryChainGeneratorInterceptorTests
{
    private const string Source = """
        using Wyrd.Ecs;

        public struct Score : IComponent { public int Value; }

        public static class Harness
        {
            public static int Run()
            {
                var world = new World();
                world.Commands.CreateEntity(new Score { Value = 5 });
                world.ApplyCommands();
                world.AdvanceTick();

                world.Query().With<Score>().ForEach(0, (in int _, ref Score s) => { s.Value += 10; });

                var observed = 0;
                world.Query().With<Score>().ForEach(0, (in int _, in Score s) => { observed = s.Value; });
                return observed;
            }
        }
        """;

    [Fact]
    public void WriteThenRead_SameShape_DifferentAccess_ReadObservesWrite()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Source));
        var method = assembly.GetType("Harness")!.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;

        var result = (int)method.Invoke(null, null)!;

        result.Should().Be(15);
    }

    [Fact]
    public void ReadOnlyCallSite_IsRoutedThroughAnInterceptor_NotThePessimisticFallback()
    {
        // Correctness alone (the test above) can pass even without interception, since
        // both call sites currently share one pessimistic all-Mut backend by coincidence.
        // This asserts the actual mechanism: the read-only call site must be redirected by
        // a generated [InterceptsLocation] method, not just happen to produce the right
        // answer through the fallback path.
        var result = GeneratorTestHost.Run(new QueryChainGenerator(), GeneratorTestHost.Compile(Source));
        var allSources = string.Join("\n---\n", result.Results[0].GeneratedSources.Select(s => s.SourceText.ToString()));

        allSources.Should().Contain("InterceptsLocation");
    }

    [Fact]
    public void TwoComponentShape_AllFourAccessVariants_EachRoutesCorrectly()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public int X; }
            public struct Velocity : IComponent { public int X; }

            public static class Harness
            {
                public static (int RefRef, int RefIn, int InRef, int InIn) Run()
                {
                    int refRef = 0, refIn = 0, inRef = 0, inIn = 0;
                    var world = new World();
                    world.Commands.CreateEntity(new Position { X = 1 }, new Velocity { X = 1 });
                    world.ApplyCommands();
                    world.AdvanceTick();

                    world.Query().With<Position>().With<Velocity>().ForEach(0, (in int _, ref Position p, ref Velocity v) => { refRef = p.X + v.X; });
                    world.Query().With<Position>().With<Velocity>().ForEach(0, (in int _, ref Position p, in Velocity v) => { refIn = p.X + v.X; });
                    world.Query().With<Position>().With<Velocity>().ForEach(0, (in int _, in Position p, ref Velocity v) => { inRef = p.X + v.X; });
                    world.Query().With<Position>().With<Velocity>().ForEach(0, (in int _, in Position p, in Velocity v) => { inIn = p.X + v.X; });

                    return (refRef, refIn, inRef, inIn);
                }
            }
            """);

        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), compilation);
        var method = assembly.GetType("Harness")!.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;

        var result = ((int, int, int, int))method.Invoke(null, null)!;

        result.Should().Be((2, 2, 2, 2));
    }

    [Fact]
    public void CollidingPredicateForEach_ReadObservesWrite_AndStopsEarlyOnFalse()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public struct Score : IComponent { public int Value; }

            public static class Harness
            {
                public static (int Observed, int Visited) Run()
                {
                    var world = new World();
                    world.Commands.CreateEntity(new Score { Value = 5 });
                    world.Commands.CreateEntity(new Score { Value = 6 });
                    world.ApplyCommands();
                    world.AdvanceTick();

                    // Colliding writer, same shape as the predicate reader below.
                    world.Query().With<Score>().ForEach(0, (in int _, ref Score s) => { s.Value += 10; });

                    var observed = 0;
                    var visited = 0;
                    world.Query().With<Score>().ForEach(0, (in int _, in Score s) =>
                    {
                        visited++;
                        observed = s.Value;
                        return false; // stop after the first entity
                    });

                    return (observed, visited);
                }
            }
            """);

        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), compilation);
        var method = assembly.GetType("Harness")!.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;

        var (observed, visited) = ((int, int))method.Invoke(null, null)!;

        observed.Should().Be(15, "the predicate's in-only read must observe the ref writer's update, same as the plain ForEach case");
        visited.Should().Be(1, "returning false must still stop iteration early through the intercepted read-only backend");
    }

    [Fact]
    public void CollidingParallelForEach_ReadObservesWrite_VisitsEveryMatchingEntity()
    {
        var compilation = GeneratorTestHost.Compile("""
            using System.Threading;
            using Wyrd.Ecs;

            public struct Score : IComponent { public int Value; }

            public static class Harness
            {
                public static (int Sum, int Visited) Run()
                {
                    var world = new World();
                    for (var i = 0; i < 50; i++)
                        world.Commands.CreateEntity(new Score { Value = 1 });
                    world.ApplyCommands();
                    world.AdvanceTick();

                    // Colliding writer, same shape as the parallel reader below.
                    world.Query().With<Score>().ForEach(0, (in int _, ref Score s) => { s.Value += 1; });

                    var sum = 0;
                    var visited = 0;
                    world.Query().With<Score>().ParallelForEach(0, (in int _, in Score s) =>
                    {
                        Interlocked.Add(ref sum, s.Value);
                        Interlocked.Increment(ref visited);
                    });

                    return (sum, visited);
                }
            }
            """);

        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), compilation);
        var method = assembly.GetType("Harness")!.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;

        var (sum, visited) = ((int, int))method.Invoke(null, null)!;

        visited.Should().Be(50);
        sum.Should().Be(100, "every entity's Score.Value must have been bumped by the ref writer (1 -> 2) before the intercepted parallel read observes it");
    }
}
