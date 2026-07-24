using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Wyrd.Ecs.Generators.Tests;

public class WorldQueryMembersGeneratorTests
{
    private static ImmutableArray<string> Run()
    {
        var compilation = CSharpCompilation.Create("Empty");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new WorldQueryMembersGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        diagnostics.Should().BeEmpty();
        return driver.GetRunResult().Results[0].GeneratedSources.Select(s => s.SourceText.ToString()).ToImmutableArray();
    }

    [Fact]
    public void IWorld_DeclaresQueryArity1()
    {
        var sources = Run();
        sources.Should().Contain(s => s.Contains("Query<T0> Query<T0>() where T0 : struct, IComponent;"));
    }

    [Fact]
    public void IWorld_DeclaresQueryMaxArity()
    {
        var sources = Run();
        var expectedWhere = string.Join(" ", Enumerable.Range(0, 8).Select(i => $"where T{i} : struct, IComponent"));
        sources.Should().Contain(s =>
            s.Contains("Query<T0, T1, T2, T3, T4, T5, T6, T7> Query<T0, T1, T2, T3, T4, T5, T6, T7>()")
            && s.Contains(expectedWhere));
    }

    [Fact]
    public void World_ImplementsQueryArity1()
    {
        var sources = Run();
        sources.Should().Contain(s => s.Contains("public Query<T0> Query<T0>() where T0 : struct, IComponent =>"));
    }

    [Fact]
    public void CommandBuffer_DeclaresCreateEntityMaxArity()
    {
        var sources = Run();
        sources.Should().Contain(s => s.Contains("public sealed partial class CommandBuffer"));
        sources.Should().Contain(s => s.Contains("CreateEntity<T0, T1, T2, T3, T4, T5, T6, T7>"));
    }
}
