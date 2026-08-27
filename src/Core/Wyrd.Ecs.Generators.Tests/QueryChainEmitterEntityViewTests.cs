using System.Collections.Immutable;

namespace Wyrd.Ecs.Generators.Tests;

public class QueryChainEmitterEntityViewTests
{
    private static QueryShape SingleWritesShape() => new()
    {
        ExactShapeTypeName = "Wyrd.Ecs.Query<(Position, Wyrd.Ecs.Nil)>",
        Markers = ImmutableArray.Create(new MarkerElement(MarkerKind.Writes, "Position")),
        PendingDataElements = ImmutableArray<string>.Empty,
    };

    [Fact]
    public void RenderForEachOverload_IncludesEntityView_DeclaresLeadingEntityViewParameterAndBuildsIt()
    {
        var source = QueryChainEmitter.RenderForEachOverload(SingleWritesShape(), includesEntityView: true);

        source.Should().Contain("EntityView entity");
        source.Should().Contain("chunk.Entities");
        source.Should().Contain("world[entities[i]]");
    }

    [Fact]
    public void RenderForEachOverload_ExcludesEntityView_NoEntityViewReferencesAtAll()
    {
        var source = QueryChainEmitter.RenderForEachOverload(SingleWritesShape(), includesEntityView: false);

        source.Should().NotContain("EntityView");
    }

    [Fact]
    public void ExactShapeHash_DiffersBetweenEntityAndNonEntityVariantsOfTheSameShape()
    {
        var shape = SingleWritesShape();

        QueryChainEmitter.ExactShapeHash(shape, includesEntityView: true)
            .Should().NotBe(QueryChainEmitter.ExactShapeHash(shape, includesEntityView: false));
    }
}
