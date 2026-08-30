using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Wyrd.Ecs.Generators.Diagnostics;

namespace Wyrd.Ecs.Generators.Tests;

public class UnusedResourceRefWriteAnalyzerTests
{
    private static ImmutableArray<Diagnostic> RunAnalyzer(string source)
    {
        var compilation = GeneratorTestHost.Compile(source);
        return compilation
            .WithAnalyzers([new UnusedResourceRefWriteAnalyzer()])
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void GetResourceRefCallNeverAssignedThrough_ReportsWYRD012()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct GameAssets : IResource { public int Value; }

            public sealed class ReaderSystem : EcsSystem
            {
                protected override void Execute(World world, Time time)
                {
                    var assets = world.GetResourceRef<GameAssets>();
                }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD012");
    }

    [Fact]
    public void GetResourceRefCallAssignedThrough_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct GameAssets : IResource { public int Value; }

            public sealed class WriterSystem : EcsSystem
            {
                protected override void Execute(World world, Time time)
                {
                    world.GetResourceRef<GameAssets>().Value = 1;
                }
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void GetResourceRefCallItselfAssigned_ReportsNothing()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct GameAssets : IResource { public int Value; }

            public sealed class WriterSystem : EcsSystem
            {
                protected override void Execute(World world, Time time)
                {
                    world.GetResourceRef<GameAssets>() = new GameAssets { Value = 1 };
                }
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void RefLocalWrittenThroughLater_ReportsNothing()
    {
        // The real pattern found in PlatformSystem's constructor: bind the ref to a local
        // first, then mutate a field/element through the local later in the same body.
        var diagnostics = RunAnalyzer("""
            using System.Collections.Generic;
            using Wyrd.Ecs;

            public struct GameAssets : IResource { public Dictionary<int, int> ById; }

            public sealed class WriterSystem : EcsSystem
            {
                protected override void Execute(World world, Time time)
                {
                    ref var assets = ref world.GetResourceRef<GameAssets>();
                    assets.ById[1] = 2;
                }
            }
            """);

        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void RefLocalNeverWrittenThrough_ReportsWYRD012()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct GameAssets : IResource { public int Value; }

            public sealed class ReaderSystem : EcsSystem
            {
                protected override void Execute(World world, Time time)
                {
                    ref var assets = ref world.GetResourceRef<GameAssets>();
                    var x = assets.Value;
                }
            }
            """);

        diagnostics.Should().ContainSingle(d => d.Id == "WYRD012");
    }

    [Fact]
    public void GetResourceCall_IsNotAnalyzed()
    {
        var diagnostics = RunAnalyzer("""
            using Wyrd.Ecs;

            public struct GameAssets : IResource { public int Value; }

            public sealed class ReaderSystem : EcsSystem
            {
                protected override void Execute(World world, Time time)
                {
                    var assets = world.GetResource<GameAssets>();
                }
            }
            """);

        diagnostics.Should().BeEmpty();
    }
}
