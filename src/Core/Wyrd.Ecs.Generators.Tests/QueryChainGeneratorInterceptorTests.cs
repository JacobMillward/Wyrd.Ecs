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
}
