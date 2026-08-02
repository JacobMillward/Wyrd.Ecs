using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Generators.Tests;

public class ChainWalkerTests
{
    private static InvocationExpressionSyntax FindForEachCall(SyntaxTree tree) =>
        tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Single(inv => inv.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ForEach" });

    [Fact]
    public void TwoBareComponentsWithRefLambda_ExtractsBothAsWritesMarkers()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            public struct Velocity : IComponent { public float X; }

            public class C
            {
                public void M(World world) =>
                    world.Query().With<Position>().With<Velocity>()
                        .ForEach(0, (in int _, ref Position p, ref Velocity v) => { });
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
    }

    [Fact]
    public void LambdaArityDoesNotMatchDeclaredComponents_ReturnsNull()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            public struct Velocity : IComponent { public float X; }

            public class C
            {
                public void M(World world) =>
                    world.Query().With<Position>().With<Velocity>()
                        .ForEach(0, (in int _, ref Position p) => { });
            }
            """);

        var terminal = FindForEachCall(compilation.SyntaxTrees[0]);
        var model = compilation.GetSemanticModel(terminal.SyntaxTree);

        ChainWalker.TryExtractShape(terminal, model, default).Should().BeNull();
    }

    [Fact]
    public void HasWithoutAnyCalls_DoNotAffectTheExtractedShape()
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
                        .With<Position>()
                        .With<Velocity>()
                        .Has<Frozen>()
                        .Without<Dead>()
                        .Any<BuffA, BuffB>()
                        .ForEach(0, (in int _, ref Position p, in Velocity v) => { });
            }
            """);

        var terminal = FindForEachCall(compilation.SyntaxTrees[0]);
        var model = compilation.GetSemanticModel(terminal.SyntaxTree);

        var shape = ChainWalker.TryExtractShape(terminal, model, default);

        // .Has/.Without/.Any never touch TShape: the walked shape only ever reflects
        // .With<T>() data elements, regardless of how many filter calls were chained in
        // between or around them.
        shape.Should().NotBeNull();
        shape!.Markers.Should().BeEquivalentTo(new[]
        {
            new MarkerElement(MarkerKind.Writes, "Position"),
            new MarkerElement(MarkerKind.Reads, "Velocity"),
        });
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
                    world.Query().With<Position>().With<Velocity>().ForEach(0, (in int _, ref Position p, in Velocity v) => { });
                public void M2(World world) =>
                    world.Query().With<Velocity>().With<Position>().ForEach(0, (in int _, in Velocity v, ref Position p) => { });
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

    [Fact]
    public void DifferentWithoutAnyCallsButSameWithSet_ProduceTheIdenticalExactShapeTypeName()
    {
        var compilation = GeneratorTestHost.Compile("""
            using Wyrd.Ecs;

            public struct Position : IComponent { public float X; }
            public struct Dead : ITag;
            public struct BuffA : ITag;
            public struct BuffB : ITag;

            public class C
            {
                public void M1(World world) =>
                    world.Query().With<Position>().Without<Dead>().ForEach(0, (in int _, in Position p) => { });
                public void M2(World world) =>
                    world.Query().With<Position>().Any<BuffA, BuffB>().ForEach(0, (in int _, in Position p) => { });
            }
            """);

        var tree = compilation.SyntaxTrees[0];
        var model = compilation.GetSemanticModel(tree);
        var terminals = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(inv => inv.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ForEach" })
            .ToList();

        var shape1 = ChainWalker.TryExtractShape(terminals[0], model, default);
        var shape2 = ChainWalker.TryExtractShape(terminals[1], model, default);

        // .Without<Dead>() and .Any<BuffA, BuffB>() both apply to Filter, never TShape, so
        // these two chains, differing only in which filter calls they made, resolve to the
        // exact same Query<TShape> closed type, and therefore share one generated backend
        // (same ExactShapeTypeName implies same HashName/DedupKey too).
        shape1.Should().NotBeNull();
        shape2.Should().NotBeNull();
        shape1!.ExactShapeTypeName.Should().Be(shape2!.ExactShapeTypeName);
        shape1.HashName().Should().Be(shape2.HashName());
    }
}
