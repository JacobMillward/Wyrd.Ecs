using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Wyrd.Ecs.Generators.Diagnostics;

namespace Wyrd.Ecs.Generators.Tests;

public class BareDataParameterAnalyzerTests
{
    private static ImmutableArray<Diagnostic> RunAnalyzer(string source)
    {
        var compilation = GeneratorTestHost.Compile(source);
        return compilation
            .WithAnalyzers([new BareDataParameterAnalyzer()])
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void ForEachLambda_BareParameter_ReportsWYRD001()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public class C
            {
                public void M(World world) =>
                    world.Query().With<Position>().ForEach(0, (in int _, Position p) => { });
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD001");
    }

    [Fact]
    public void ForEachLambda_AllParametersAnnotated_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public class C
            {
                public void M(World world) =>
                    world.Query().With<Position>().ForEach(0, (in int _, ref Position p) => { });
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "WYRD001");
    }

    [Fact]
    public void ForEachLambda_NoUniformOverload_BareParameter_ReportsWYRD001()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public class C
            {
                public void M(World world) =>
                    world.Query().With<Position>().ForEach((Position p) => { });
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD001");
    }

    [Fact]
    public void ForEachLambda_NoUniformOverload_Annotated_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public class C
            {
                public void M(World world) =>
                    world.Query().With<Position>().ForEach((ref Position p) => { });
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "WYRD001");
    }

    [Fact]
    public void ForEachLambda_LeadingEntityView_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public class C
            {
                public void M(World world) =>
                    world.Query().With<Position>().ForEach(0, (in int _, EntityView entity, ref Position p) => { });
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "WYRD001");
    }

    [Fact]
    public void ForEachLambda_NoUniformOverload_LeadingEntityView_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public class C
            {
                public void M(World world) =>
                    world.Query().With<Position>().ForEach((EntityView entity, ref Position p) => { });
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "WYRD001");
    }

    [Fact]
    public void ForEachLambda_BareParameterAfterEntityView_ReportsWYRD001()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public class C
            {
                public void M(World world) =>
                    world.Query().With<Position>().ForEach(0, (in int _, EntityView entity, Position p) => { });
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD001");
    }

    [Fact]
    public void ForEachLambda_EntityViewNotInLeadingPosition_StillFlaggedAsBare()
    {
        // EntityView is only recognized immediately after the uniform-state parameters; here
        // it trails a data parameter instead, so it's just another bare parameter.
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public class C
            {
                public void M(World world) =>
                    world.Query().With<Position>().ForEach(0, (in int _, ref Position p, EntityView entity) => { });
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD001");
    }

    [Fact]
    public void QuerySystemUpdate_BareParameter_ReportsWYRD001()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public sealed class BrokenSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Position>();

                public void Update(Time time, Position p) { }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD001");
    }

    [Fact]
    public void QuerySystemUpdate_AllParametersAnnotated_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public sealed class WorkingSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Position>();

                public void Update(Time time, ref Position p) { }
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "WYRD001");
    }

    [Fact]
    public void QuerySystemUpdate_WorldAndEntityViewParameters_ReportNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public sealed class WorkingSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Position>();

                public void Update(Time time, World world, EntityView entity, ref Position p) { }
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "WYRD001");
    }

    [Fact]
    public void QuerySystemUpdate_BareComponentAfterWorldAndEntityView_ReportsWYRD001()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }

            public sealed class BrokenSystem : QuerySystem
            {
                protected override IQuery DefineQuery(Query query) => query.With<Position>();

                public void Update(Time time, World world, EntityView entity, Position p) { }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD001");
    }
}
