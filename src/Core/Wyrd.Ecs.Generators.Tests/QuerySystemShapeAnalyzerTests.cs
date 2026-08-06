using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Wyrd.Ecs.Generators.Diagnostics;

namespace Wyrd.Ecs.Generators.Tests;

public class QuerySystemShapeAnalyzerTests
{
    private static ImmutableArray<Diagnostic> RunAnalyzer(string source)
    {
        var compilation = GeneratorTestHost.Compile(source);
        return compilation
            .WithAnalyzers([new QuerySystemShapeAnalyzer()])
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void MatchingUpdate_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            public struct Velocity : IComponent { public float X; }

            public sealed class MovementSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Position>().With<Velocity>();

                public void Update(Time time, ref Position p, in Velocity v) { }
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "WYRD002");
    }

    [Fact]
    public void MissingUpdate_ReportsWYRD002()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public sealed class BrokenSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Position>();
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD002");
    }

    [Fact]
    public void WrongComponentCount_ReportsWYRD002()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            public struct Velocity : IComponent { public float X; }

            public sealed class BrokenSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Position>().With<Velocity>();

                public void Update(Time time, ref Position p) { }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD002");
    }

    [Fact]
    public void WrongOrder_ReportsWYRD002()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            public struct Velocity : IComponent { public float X; }

            public sealed class BrokenSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Position>().With<Velocity>();

                public void Update(Time time, ref Velocity v, ref Position p) { }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD002");
    }

    [Fact]
    public void UpdateWithWorldParameter_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public sealed class MovementSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Position>();

                public void Update(Time time, World world, ref Position p) { }
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "WYRD002");
    }

    [Fact]
    public void UpdateWithEntityViewParameter_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public sealed class MovementSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Position>();

                public void Update(Time time, EntityView entity, ref Position p) { }
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "WYRD002");
    }

    [Fact]
    public void UpdateWithWorldAndEntityViewParameters_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public sealed class MovementSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Position>();

                public void Update(Time time, World world, EntityView entity, ref Position p) { }
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "WYRD002");
    }

    [Fact]
    public void WorldDeclaredAfterEntityView_ReportsWYRD002()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public sealed class BrokenSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Position>();

                public void Update(Time time, EntityView entity, World world, ref Position p) { }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD002");
    }
}
