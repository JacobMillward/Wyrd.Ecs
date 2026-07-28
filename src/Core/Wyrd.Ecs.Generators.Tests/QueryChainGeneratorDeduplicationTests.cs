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

        backendClassNames.Should().ContainSingle("both call sites declare the same logical shape, just in a different order, and must share one backend");
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
    public void SameExactShapeTypeName_ConflictingRefInResolution_ReportsWYRD003()
    {
        // Both call sites declare the identical .With<Position>() set (so the identical
        // Query<(Position, Nil)> closed type), but one writes it and one only reads it --
        // since .Without/.Has/.Any no longer affect TShape, nothing else distinguishes them.
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

        result.Diagnostics.Should().ContainSingle(d => d.Id == "WYRD003");
    }
}
