using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests.Internal;

public class RelationTraitsTests
{
    private struct Likes : IRelation;

    private struct Parent : IExclusiveRelation;

    [Fact]
    public void PlainRelation_IsNotExclusive()
    {
        RelationTraits<Likes>.IsExclusive.Should().BeFalse();
    }

    [Fact]
    public void ExclusiveRelation_IsExclusive()
    {
        RelationTraits<Parent>.IsExclusive.Should().BeTrue();
    }
}
