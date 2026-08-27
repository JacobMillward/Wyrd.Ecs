using System.Text.RegularExpressions;

namespace Wyrd.Ecs.Generators.Tests;

public class QueryChainGeneratorDeduplicationTests
{
    [Fact]
    public void SameLogicalShape_DifferentDeclarationOrder_DifferentFiles_SharesOneBackend()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            public struct Velocity : IComponent { public float X; }

            public static class SiteA
            {
                public static void M(World world) =>
                    world.Query().With<Position>().With<Velocity>().ForEach(0, (in int _, ref Position p, in Velocity v) => { });
            }

            public static class SiteB
            {
                public static void M(World world) =>
                    world.Query().With<Velocity>().With<Position>().ForEach(0, (in int _, in Velocity v, ref Position p) => { });
            }
            """);

        var result = GeneratorTestHost.Run(new QueryChainGenerator(), compilation);
        var allSources = string.Join("\n---\n", result.Results[0].GeneratedSources.Select(s => s.SourceText.ToString()));

        var backendClassNames = Regex.Matches(allSources, @"internal static class (QueryChainBackend_\w+)")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        // Two, not one: SiteA/SiteB's own real access variant (ref Position, in Velocity)
        // is order-independent and shares one backend, exactly as before -- but this shape
        // has a Reads marker, so a second, synthesized all-Writes canonical backend also
        // exists (the public overload's pessimistic fallback). If order-sharing broke,
        // this would be 3, not 2.
        backendClassNames.Should().HaveCount(2, "the real variant backend is still shared across declaration order, plus one canonical all-Writes fallback backend for this shape");
    }

    [Fact]
    public void DifferentLogicalShapes_GetSeparateBackends()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            public struct Velocity : IComponent { public float X; }
            public struct Health : IComponent { public float Current; }

            public static class SiteA
            {
                public static void M(World world) =>
                    world.Query().With<Position>().ForEach(0, (in int _, ref Position p) => { });
            }

            public static class SiteB
            {
                public static void M(World world) =>
                    world.Query().With<Health>().ForEach(0, (in int _, ref Health h) => { });
            }
            """);

        var result = GeneratorTestHost.Run(new QueryChainGenerator(), compilation);
        var allSources = string.Join("\n---\n", result.Results[0].GeneratedSources.Select(s => s.SourceText.ToString()));

        var backendClassNames = Regex.Matches(allSources, @"internal static class (QueryChainBackend_\w+)")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        backendClassNames.Should().HaveCount(2, "Position-only and Health-only are genuinely different shapes");
    }

    [Fact]
    public void SameExactShapeTypeName_ConflictingRefInResolution_NoDiagnostic_BothCallSitesCompile()
    {
        // Both call sites declare the identical .With<Position>() set (so the identical
        // Query<(Position, Nil)> closed type), but one writes it and one only reads it.
        // Since .Without/.Has/.Any don't affect TShape, nothing else distinguishes them.
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public static class SiteA
            {
                public static void M(World world) =>
                    world.Query().With<Position>().ForEach(0, (in int _, ref Position p) => { });
            }

            public static class SiteB
            {
                public static void M(World world) =>
                    world.Query().With<Position>().ForEach(0, (in int _, in Position p) => { });
            }
            """);

        var result = GeneratorTestHost.Run(new QueryChainGenerator(), compilation);

        result.Diagnostics.Where(d => d.Id == "WYRD003").Should().BeEmpty();
    }
}
