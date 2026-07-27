using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Generators.Tests;

public class ChainWalkerTests
{
    private static InvocationExpressionSyntax FindForEachCall(SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(inv => inv.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ForEach" });

    [Fact]
    public void TwoWrites_ExtractsBothAsWritesMarkers()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            public struct Velocity : IComponent { public float X; }

            public class C
            {
                public void M(World world) =>
                    world.Query().With<Writes<Position>>().With<Writes<Velocity>>().ForEach(0, DummyCallback);

                private static void DummyCallback() { }
            }
            """);

        var terminal = FindForEachCall(compilation.SyntaxTrees[0]);
        var model = compilation.GetSemanticModel(terminal.SyntaxTree);

        var shape = ChainWalker.TryExtractShape(terminal, model, default);

        shape.Should().NotBeNull();
        shape!.Markers.Should().BeEquivalentTo(new[]
        {
            new MarkerElement(MarkerKind.Writes, "Position"),
            new MarkerElement(MarkerKind.Writes, "Velocity"),
        });
        shape.Withouts.Should().BeEmpty();
        shape.Anys.Should().BeEmpty();
    }

    [Fact]
    public void MixedWritesReadsHasWithoutAny_ClassifiesEachCorrectly()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            public struct Velocity : IComponent { public float X; }
            public struct Dead : ITag;
            public struct BuffA : ITag;
            public struct BuffB : ITag;
            public struct Frozen : ITag;

            public class C
            {
                public void M(World world) =>
                    world.Query()
                        .With<Writes<Position>>()
                        .With<Reads<Velocity>>()
                        .With<Has<Frozen>>()
                        .Without<Dead>()
                        .Any<BuffA, BuffB>()
                        .ForEach(0, DummyCallback);

                private static void DummyCallback() { }
            }
            """);

        var terminal = FindForEachCall(compilation.SyntaxTrees[0]);
        var model = compilation.GetSemanticModel(terminal.SyntaxTree);

        var shape = ChainWalker.TryExtractShape(terminal, model, default);

        shape.Should().NotBeNull();
        shape!.Markers.Should().BeEquivalentTo(new[]
        {
            new MarkerElement(MarkerKind.Writes, "Position"),
            new MarkerElement(MarkerKind.Reads, "Velocity"),
            new MarkerElement(MarkerKind.Has, "Frozen"),
        });
        shape.Withouts.Should().BeEquivalentTo(new[] { new WithoutElement("Dead") });
        shape.Anys.Should().BeEquivalentTo(new[] { new AnyElement("BuffA", "BuffB") });
    }

    [Fact]
    public void NoFilters_ExtractsAnEmptyShape()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public class C
            {
                public void M(World world) => world.Query().ForEach(0, DummyCallback);

                private static void DummyCallback() { }
            }
            """);

        var terminal = FindForEachCall(compilation.SyntaxTrees[0]);
        var model = compilation.GetSemanticModel(terminal.SyntaxTree);

        var shape = ChainWalker.TryExtractShape(terminal, model, default);

        shape.Should().NotBeNull();
        shape!.Markers.Should().BeEmpty();
        shape.Withouts.Should().BeEmpty();
        shape.Anys.Should().BeEmpty();
    }

    [Fact]
    public void UnrelatedForEachCall_ReturnsNull()
    {
        var compilation = GeneratorTestHost.Compile("""
            using System.Collections.Generic;

            public class C
            {
                public void M(List<int> list) => list.ForEach(x => { });
            }
            """);

        var terminal = FindForEachCall(compilation.SyntaxTrees[0]);
        var model = compilation.GetSemanticModel(terminal.SyntaxTree);

        var shape = ChainWalker.TryExtractShape(terminal, model, default);

        shape.Should().BeNull();
    }

    [Fact]
    public void DifferentDeclarationOrder_ProducesTheSameDedupKeyButDifferentExactShapeTypeName()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            public struct Velocity : IComponent { public float X; }

            public class C
            {
                public void M1(World world) =>
                    world.Query().With<Writes<Position>>().With<Reads<Velocity>>().ForEach(0, D);
                public void M2(World world) =>
                    world.Query().With<Reads<Velocity>>().With<Writes<Position>>().ForEach(0, D);

                private static void D() { }
            }
            """);

        var tree = compilation.SyntaxTrees[0];
        var model = compilation.GetSemanticModel(tree);
        var terminals = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ForEach" })
            .ToList();

        var shape1 = ChainWalker.TryExtractShape(terminals[0], model, default);
        var shape2 = ChainWalker.TryExtractShape(terminals[1], model, default);

        shape1.Should().NotBeNull();
        shape2.Should().NotBeNull();
        shape1!.DedupKey().Should().Be(shape2!.DedupKey());
        shape1.ExactShapeTypeName.Should().NotBe(shape2.ExactShapeTypeName);
    }
}
