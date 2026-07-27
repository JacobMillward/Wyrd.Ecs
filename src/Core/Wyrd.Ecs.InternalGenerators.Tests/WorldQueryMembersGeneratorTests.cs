using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Wyrd.Ecs.InternalGenerators.Tests;

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
    public void CommandBuffer_DeclaresCreateEntityMaxArity()
    {
        var sources = Run();
        sources.Should().Contain(s => s.Contains("public sealed partial class CommandBuffer"));
        sources.Should().Contain(s => s.Contains("CreateEntity<T0, T1, T2, T3, T4, T5, T6, T7>"));
    }

    [Fact]
    public void World_StillEmitsPlaceReservedEntityAndItsQuerySignatureCache()
    {
        // PlaceReservedEntity (entity creation, unrelated to querying) still needs a
        // cached archetype-signature lookup to find or create its target archetype --
        // QuerySignature<T0,...> stays for this reason even though the fluent
        // Query<T0,...> family that originally shared it is gone.
        var sources = Run();
        sources.Should().Contain(s => s.Contains("internal void PlaceReservedEntity<T0>"));
        sources.Should().Contain(s => s.Contains("internal static class QuerySignature<T0>"));
        sources.Should().Contain(s => s.Contains("var signature = QuerySignature<T0>.Value;"));
    }
}
