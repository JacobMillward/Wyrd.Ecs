using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests.Internal;

file struct Alpha : IComponent;
file struct Gamma : ITag;
file struct Delta : ITag;

public class QueryFilterTests
{
    [Fact]
    public void Empty_MatchesAnyArchetype()
    {
        var signature = ArchetypeSignature.Empty.With(TypeIndex<Alpha>.Value);

        QueryFilter.Empty.Matches(signature).Should().BeTrue();
    }

    [Fact]
    public void Has_RequiresPresence()
    {
        var filter = QueryFilter.Empty.Has<Gamma>();
        var withGamma = ArchetypeSignature.Empty.With(TypeIndex<Gamma>.Value);
        var withoutGamma = ArchetypeSignature.Empty.With(TypeIndex<Alpha>.Value);

        filter.Matches(withGamma).Should().BeTrue();
        filter.Matches(withoutGamma).Should().BeFalse();
    }

    [Fact]
    public void Without_ExcludesPresence()
    {
        var filter = QueryFilter.Empty.Without<Delta>();
        var withDelta = ArchetypeSignature.Empty.With(TypeIndex<Delta>.Value);
        var withoutDelta = ArchetypeSignature.Empty.With(TypeIndex<Alpha>.Value);

        filter.Matches(withDelta).Should().BeFalse();
        filter.Matches(withoutDelta).Should().BeTrue();
    }

    [Fact]
    public void Any_RequiresAtLeastOne()
    {
        var filter = QueryFilter.Empty.Any<Gamma, Delta>();
        var withGammaOnly = ArchetypeSignature.Empty.With(TypeIndex<Gamma>.Value);
        var withNeither = ArchetypeSignature.Empty.With(TypeIndex<Alpha>.Value);

        filter.Matches(withGammaOnly).Should().BeTrue();
        filter.Matches(withNeither).Should().BeFalse();
    }

    [Fact]
    public void CombinedHasWithoutAny_AllMustHold()
    {
        var filter = QueryFilter.Empty.Has<Alpha>().Without<Delta>().Any<Gamma, Delta>();
        var violatesWithout = ArchetypeSignature.Empty
            .With(TypeIndex<Alpha>.Value).With(TypeIndex<Delta>.Value);
        var satisfiesAll = ArchetypeSignature.Empty
            .With(TypeIndex<Alpha>.Value).With(TypeIndex<Gamma>.Value);

        filter.Matches(violatesWithout).Should().BeFalse();
        filter.Matches(satisfiesAll).Should().BeTrue();
    }

    [Fact]
    public void Equals_ComparesByValue()
    {
        var a = QueryFilter.Empty.Has<Alpha>().Without<Delta>();
        var b = QueryFilter.Empty.Has<Alpha>().Without<Delta>();

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
