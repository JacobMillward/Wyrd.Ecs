using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Wyrd.Ecs.Generators.Tests;

public class QueryTypesGeneratorTests
{
    private static ImmutableArray<string> Run()
    {
        var compilation = CSharpCompilation.Create("Empty");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new QueryTypesGenerator().AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        diagnostics.Should().BeEmpty();
        return driver.GetRunResult().Results[0].GeneratedSources.Select(s => s.SourceText.ToString()).ToImmutableArray();
    }

    [Fact]
    public void EmitsQueryRowArity1()
    {
        var sources = Run();
        sources.Should().Contain(s => s.Contains("public readonly ref struct QueryRow<T0>"));
    }

    [Fact]
    public void EmitsQueryRowMaxArity()
    {
        var sources = Run();
        sources.Should().Contain(s => s.Contains("public readonly ref struct QueryRow<T0, T1, T2, T3, T4, T5, T6, T7>"));
    }

    [Fact]
    public void EmitsQueryArity1()
    {
        var sources = Run();
        sources.Should().Contain(s => s.Contains("public readonly ref struct Query<T0>"));
    }

    [Fact]
    public void EmitsQueryMaxArity()
    {
        var sources = Run();
        sources.Should().Contain(s => s.Contains("public readonly ref struct Query<T0, T1, T2, T3, T4, T5, T6, T7>"));
    }

    [Fact]
    public void EmitsExactlyNineArities()
    {
        // 8 arities (1..8) for each of QueryRow, Query, QuerySignature, QuerySystem = 32 template
        // expansions, but they land in only 4 files (one per type family). This just guards
        // against QueryArity.Max silently changing without anyone noticing.
        var sources = Run();
        var queryRowFile = sources.Single(s => s.Contains("public readonly ref struct QueryRow<T0>"));
        for (var n = 1; n <= 8; n++)
        {
            var typeParams = string.Join(", ", Enumerable.Range(0, n).Select(i => $"T{i}"));
            queryRowFile.Should().Contain($"public readonly ref struct QueryRow<{typeParams}>");
        }
        queryRowFile.Should().NotContain("T8");
    }
}
