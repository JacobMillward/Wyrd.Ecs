using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Wyrd.Ecs.Input.Generators.Tests;

public class FixedCadenceEdgeReadAnalyzerTests
{
    private static ImmutableArray<Diagnostic> RunAnalyzer(string source)
    {
        var compilation = GeneratorTestHost.Compile(source);
        return compilation
            .WithAnalyzers([new FixedCadenceEdgeReadAnalyzer()])
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void FixedTimestepSystem_ReadingJustPressed_ReportsWYRD011()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;
            using Wyrd.Ecs.Input;

            public enum Action { Fire }

            [FixedTimestep]
            public sealed class S : EcsSystem
            {
                private IntentState<Action> _input;
                protected override void Execute(World world, Time time)
                {
                    if (_input[Action.Fire].JustPressed) { }
                }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD011");
    }

    [Fact]
    public void FixedTimestepSystem_ReadingJustReleased_ReportsWYRD011()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;
            using Wyrd.Ecs.Input;

            public enum Action { Fire }

            [FixedTimestep]
            public sealed class S : EcsSystem
            {
                private IntentState<Action> _input;
                protected override void Execute(World world, Time time)
                {
                    if (_input[Action.Fire].JustReleased) { }
                }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD011");
    }

    [Fact]
    public void FixedTimestepSystem_ReadingTickJustPressed_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;
            using Wyrd.Ecs.Input;

            public enum Action { Fire }

            [FixedTimestep]
            public sealed class S : EcsSystem
            {
                private IntentState<Action> _input;
                protected override void Execute(World world, Time time)
                {
                    if (_input[Action.Fire].TickJustPressed) { }
                }
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "WYRD011");
    }

    [Fact]
    public void VariableCadenceSystem_ReadingJustPressed_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;
            using Wyrd.Ecs.Input;

            public enum Action { Fire }

            public sealed class S : EcsSystem
            {
                private IntentState<Action> _input;
                protected override void Execute(World world, Time time)
                {
                    if (_input[Action.Fire].JustPressed) { }
                }
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "WYRD011");
    }

    [Fact]
    public void FixedTimestepSystem_ReadingUnrelatedTypesJustPressedProperty_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            [FixedTimestep]
            public sealed class S : EcsSystem
            {
                private struct NotActionState { public bool JustPressed; }
                private NotActionState _other;
                protected override void Execute(World world, Time time)
                {
                    if (_other.JustPressed) { }
                }
            }
            """);

        diagnostics.Should().NotContain(d => d.Id == "WYRD011");
    }
}
