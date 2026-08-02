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
        // PlaceReservedEntity (entity creation, unrelated to querying) needs a cached
        // archetype-signature lookup to find or create its target archetype, which is
        // what QuerySignature<T0,...> provides.
        var sources = Run();
        sources.Should().Contain(s => s.Contains("internal void PlaceReservedEntity<T0>"));
        sources.Should().Contain(s => s.Contains("internal static class QuerySignature<T0>"));
        sources.Should().Contain(s => s.Contains("var signature = QuerySignature<T0>.Value;"));
    }

    [Fact]
    public void CommandBuffer_DeclaresBatchCreateEntityMaxArity()
    {
        var sources = Run();
        sources.Should().Contain(s => s.Contains("public sealed partial class CommandBuffer"));
        sources.Should().Contain(s => s.Contains("Entity[] CreateEntity<T0, T1, T2, T3, T4, T5, T6, T7>(int count"));
    }

    [Fact]
    public void World_EmitsPlaceReservedEntitiesForBatchCreation()
    {
        var sources = Run();
        sources.Should().Contain(s => s.Contains("internal void PlaceReservedEntities<T0>(Entity[] entities, T0 component0)"));
    }

    [Fact]
    public void CommandBuffer_EmitsBatchCreateEntityOpDispatcher()
    {
        var sources = Run();
        sources.Should().Contain(s => s.Contains("file static class BatchCreateEntityOp<T0>"));
    }

    [Fact]
    public void CommandBuffer_SingleEntityCreateEntity_ReturnsEntityView()
    {
        var sources = Run();
        sources.Should().Contain(s => s.Contains("public EntityView CreateEntity<T0, T1, T2, T3, T4, T5, T6, T7>"));
    }
}
