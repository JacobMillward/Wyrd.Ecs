using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests.Internal;

public class TypeIndexTests
{
    private struct MarkerA
    {
    }

    private struct MarkerB
    {
    }

    [Fact]
    public void SameType_ReturnsSameIndexOnRepeatedAccess()
    {
        var first = TypeIndex<MarkerA>.Value;

        TypeIndex<MarkerA>.Value.Should().Be(first);
    }

    [Fact]
    public void DifferentTypes_ReturnDistinctIndices()
    {
        TypeIndex<MarkerA>.Value.Should().NotBe(TypeIndex<MarkerB>.Value);
    }
}
