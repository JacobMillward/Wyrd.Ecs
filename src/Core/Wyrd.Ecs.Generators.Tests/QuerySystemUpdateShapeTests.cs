using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Wyrd.Ecs.Generators.Tests;

public class QuerySystemUpdateShapeTests
{
    private static ImmutableArray<IParameterSymbol> ParametersOf(string source)
    {
        var compilation = GeneratorTestHost.Compile(source);
        var tree = compilation.SyntaxTrees.Single();
        var semanticModel = compilation.GetSemanticModel(tree);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "Update");
        return ((IMethodSymbol)semanticModel.GetDeclaredSymbol(method)!).Parameters;
    }

    [Fact]
    public void TimeOnly_IsValid()
    {
        var parameters = ParametersOf("""
            using Wyrd.Ecs;
            public struct Position : IComponent { public float X; }
            public sealed class S : QuerySystem
            {
                protected override IQuery DefineQuery(World world) => world.Query().With<Position>();
                public void Update(Time time, ref Position p) { }
            }
            """);

        var result = QuerySystemUpdateShape.Classify(parameters);

        result.IsValid.Should().BeTrue();
        result.HasWorld.Should().BeFalse();
        result.HasEntityView.Should().BeFalse();
        result.ComponentStartIndex.Should().Be(1);
    }

    [Fact]
    public void TimeThenWorld_IsValid()
    {
        var parameters = ParametersOf("""
            using Wyrd.Ecs;
            public struct Position : IComponent { public float X; }
            public sealed class S : QuerySystem
            {
                protected override IQuery DefineQuery(World world) => world.Query().With<Position>();
                public void Update(Time time, World world, ref Position p) { }
            }
            """);

        var result = QuerySystemUpdateShape.Classify(parameters);

        result.IsValid.Should().BeTrue();
        result.HasWorld.Should().BeTrue();
        result.HasEntityView.Should().BeFalse();
        result.ComponentStartIndex.Should().Be(2);
    }

    [Fact]
    public void TimeThenEntityView_IsValid()
    {
        var parameters = ParametersOf("""
            using Wyrd.Ecs;
            public struct Position : IComponent { public float X; }
            public sealed class S : QuerySystem
            {
                protected override IQuery DefineQuery(World world) => world.Query().With<Position>();
                public void Update(Time time, EntityView entity, ref Position p) { }
            }
            """);

        var result = QuerySystemUpdateShape.Classify(parameters);

        result.IsValid.Should().BeTrue();
        result.HasWorld.Should().BeFalse();
        result.HasEntityView.Should().BeTrue();
        result.ComponentStartIndex.Should().Be(2);
    }

    [Fact]
    public void TimeThenWorldThenEntityView_IsValid()
    {
        var parameters = ParametersOf("""
            using Wyrd.Ecs;
            public struct Position : IComponent { public float X; }
            public sealed class S : QuerySystem
            {
                protected override IQuery DefineQuery(World world) => world.Query().With<Position>();
                public void Update(Time time, World world, EntityView entity, ref Position p) { }
            }
            """);

        var result = QuerySystemUpdateShape.Classify(parameters);

        result.IsValid.Should().BeTrue();
        result.HasWorld.Should().BeTrue();
        result.HasEntityView.Should().BeTrue();
        result.ComponentStartIndex.Should().Be(3);
    }

    [Fact]
    public void EntityViewBeforeWorld_StopsRecognizingAtEntityView()
    {
        // Canonical order is Time -> World -> EntityView. EntityView declared before World
        // means classification only recognizes Time+EntityView; the World parameter that
        // follows is left in the component range, where component-shape matching (Task 2/3)
        // rejects it naturally since "World" never matches a declared component type name.
        var parameters = ParametersOf("""
            using Wyrd.Ecs;
            public struct Position : IComponent { public float X; }
            public sealed class S : QuerySystem
            {
                protected override IQuery DefineQuery(World world) => world.Query().With<Position>();
                public void Update(Time time, EntityView entity, World world, ref Position p) { }
            }
            """);

        var result = QuerySystemUpdateShape.Classify(parameters);

        result.IsValid.Should().BeTrue();
        result.HasWorld.Should().BeFalse();
        result.HasEntityView.Should().BeTrue();
        result.ComponentStartIndex.Should().Be(2);
    }

    [Fact]
    public void MissingTime_IsInvalid()
    {
        var parameters = ParametersOf("""
            using Wyrd.Ecs;
            public struct Position : IComponent { public float X; }
            public sealed class S : QuerySystem
            {
                protected override IQuery DefineQuery(World world) => world.Query().With<Position>();
                public void Update(World world, ref Position p) { }
            }
            """);

        QuerySystemUpdateShape.Classify(parameters).IsValid.Should().BeFalse();
    }

    [Fact]
    public void NoParameters_IsInvalid()
    {
        QuerySystemUpdateShape.Classify(ImmutableArray<IParameterSymbol>.Empty).IsValid.Should().BeFalse();
    }
}
