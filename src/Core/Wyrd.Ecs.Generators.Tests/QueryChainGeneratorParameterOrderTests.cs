namespace Wyrd.Ecs.Generators.Tests;

/// <summary>
/// The shared backend sorts a shape's Reads/Writes elements alphabetically by component
/// type name so two call sites declaring the same components in a different
/// `.With&lt;&gt;()` order still share one backend. That's an internal sharing detail: a
/// caller's `.ForEach(...)` lambda should be able to use whatever order they actually
/// declared, not the shared backend's alphabetical order.
/// </summary>
public class QueryChainGeneratorParameterOrderTests
{
    private const string Harness = """
        using Wyrd.Ecs;

        public struct Position : IComponent { public float X; }
        public struct Velocity : IComponent { public float X; }
        public struct Health : IComponent { public float Current; }

        public static class Harness
        {
            // Reuses Position/Velocity in a two-component shape, so the three-component shape
            // below shares a backend-relevant component set with another shape in the same
            // compilation, exercising the lambda-parameter-order-vs-backend-order distinction.
            public static void RunTwoComponent(World world) =>
                world.Query().With<Position>().With<Velocity>()
                    .ForEach(0, (in int _, ref Position p, in Velocity v) => p.X += v.X);

            // Lambda parameters follow the .With<>() declaration order (Position, Velocity,
            // Health), not alphabetical-by-type-name order (Health, Position, Velocity).
            public static float RunThreeComponent()
            {
                var world = new World();
                world.Commands.CreateEntity(new Position { X = 1f }, new Velocity { X = 2f }, new Health { Current = 100f });
                world.ApplyCommands();
                RunTwoComponent(world);

                var total = 0f;
                world.Query().With<Position>().With<Velocity>().With<Health>()
                    .ForEach(0, (in int _, in Position p, in Velocity v, ref Health h) =>
                    {
                        h.Current += p.X + v.X;
                        total = h.Current;
                    });

                return total;
            }
        }
        """;

    [Fact]
    public void ForEach_LambdaParametersInDeclarationOrder_CompilesAndExecutesCorrectly_EvenWhenComponentsShareABackendWithAnotherShape()
    {
        var assembly = GeneratorTestHost.CompileAndLoad(new QueryChainGenerator(), GeneratorTestHost.Compile(Harness));

        var result = (float)assembly.GetType("Harness")!.GetMethod("RunThreeComponent")!.Invoke(null, null)!;

        // Position.X (1) doubled by RunTwoComponent (1 + 2 = 3) is applied first, then the
        // three-component pass reads Position.X=3, Velocity.X=2, adds both to Health.Current=100.
        result.Should().Be(105f);
    }
}
