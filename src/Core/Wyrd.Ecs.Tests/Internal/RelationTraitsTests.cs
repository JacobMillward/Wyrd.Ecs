using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests.Internal;

public class RelationTraitsTests
{
    private struct Likes : IRelation;

    private struct Parent : IExclusiveRelation;

    private struct Dependent : IDependent;

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

    [Fact]
    public void PlainRelation_IsNotDependent()
    {
        RelationTraits<Likes>.IsDependent.Should().BeFalse();
    }

    [Fact]
    public void DependentRelation_IsDependent()
    {
        RelationTraits<Dependent>.IsDependent.Should().BeTrue();
    }
}
