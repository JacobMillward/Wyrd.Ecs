namespace Wyrd.Ecs.Generators.Tests;

public class FileLocalComponentTypeTests
{
    [Fact]
    public void FileScopedComponentInForEachCall_ReportsWYRD004()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            file struct Position : IComponent { public float X; }

            public class C
            {
                public void M(World world) =>
                    world.Query().With<Position>().ForEach(0, (in int _, ref Position p) => { });
            }
            """);

        var result = GeneratorTestHost.Run(new QueryChainGenerator(), compilation);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "WYRD004");
    }

    [Fact]
    public void FileScopedComponentInQuerySystemDefineQuery_ReportsWYRD004()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            file struct Position : IComponent { public float X; }

            public sealed partial class MoveSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Position>();

                public void Update(Time time, ref Position p) { }
            }
            """);

        var result = GeneratorTestHost.Run(new QueryChainGenerator(), compilation);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "WYRD004");
    }

    [Fact]
    public void FileScopedTypeCollidingWithOrdinarySameNamedTypeElsewhere_ReportsWYRD004_DoesNotCorruptTheOrdinaryOnesShape()
    {
        // Two separate "files" (syntax trees): FileA declares an ordinary Position and reads it;
        // FileB declares an unrelated file-scoped Position and writes it. FileB's file-scoped
        // type is rejected outright, and FileA's own query must still resolve correctly, not
        // corrupted by the rejected one.
        var fileA = """
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public static class SiteA
            {
                public static void M(World world) =>
                    world.Query().With<Position>().ForEach(0, (in int _, in Position p) => { });
            }
            """;
        var fileB = """
            using Wyrd.Ecs;

            file struct Position : IComponent { public float X; }

            public static class SiteB
            {
                public static void M(World world) =>
                    world.Query().With<Position>().ForEach(0, (in int _, ref Position p) => { });
            }
            """;

        var compilation = GeneratorTestHost.Compile(fileA, fileB);
        var result = GeneratorTestHost.Run(new QueryChainGenerator(), compilation);

        result.Diagnostics.Should().ContainSingle(d => d.Id == "WYRD004");

        var allSources = string.Join("\n---\n", result.Results[0].GeneratedSources.Select(s => s.SourceText.ToString()));
        // SiteA's own real access variant is read-only (in Position); its backend must
        // still build a Ref<Position> accessor, not a Mut<Position> one, unaffected by
        // SiteB's rejected file-scoped type. (The public overload's own delegate is
        // always the canonical all-ref shape now, so checking for "in Position p" in a
        // delegate signature would no longer distinguish this.)
        allSources.Should().Contain("Access<Ref<Position>>()", "SiteA's read-only shape must still be generated correctly, unaffected by SiteB's rejected file-scoped type");
    }
}
