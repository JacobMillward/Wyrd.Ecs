using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests;

file struct Alpha : IComponent;
file struct Gamma : ITag;
file struct Delta : ITag;
file struct Epsilon : ITag;

public class ArchetypeFilterTests
{
    [Fact]
    public void Empty_MatchesAnyArchetype()
    {
        var signature = TypeBitSet.Empty.With(TypeIndex<Alpha>.Value);

        ArchetypeFilter.Empty.Matches(signature).Should().BeTrue();
    }

    [Fact]
    public void Has_RequiresPresence()
    {
        var filter = ArchetypeFilter.Empty.Has<Gamma>();
        var withGamma = TypeBitSet.Empty.With(TypeIndex<Gamma>.Value);
        var withoutGamma = TypeBitSet.Empty.With(TypeIndex<Alpha>.Value);

        filter.Matches(withGamma).Should().BeTrue();
        filter.Matches(withoutGamma).Should().BeFalse();
    }

    [Fact]
    public void Without_ExcludesPresence()
    {
        var filter = ArchetypeFilter.Empty.Without<Delta>();
        var withDelta = TypeBitSet.Empty.With(TypeIndex<Delta>.Value);
        var withoutDelta = TypeBitSet.Empty.With(TypeIndex<Alpha>.Value);

        filter.Matches(withDelta).Should().BeFalse();
        filter.Matches(withoutDelta).Should().BeTrue();
    }

    [Fact]
    public void Any_RequiresAtLeastOne()
    {
        var filter = ArchetypeFilter.Empty.Any<Gamma, Delta>();
        var withGammaOnly = TypeBitSet.Empty.With(TypeIndex<Gamma>.Value);
        var withNeither = TypeBitSet.Empty.With(TypeIndex<Alpha>.Value);

        filter.Matches(withGammaOnly).Should().BeTrue();
        filter.Matches(withNeither).Should().BeFalse();
    }

    [Fact]
    public void TwoIndependentAnyGroups_BothMustBeSatisfied()
    {
        var filter = ArchetypeFilter.Empty.Any<Gamma, Delta>().Any<Alpha, Epsilon>();

        var satisfiesOnlyFirstGroup = TypeBitSet.Empty.With(TypeIndex<Gamma>.Value);
        var satisfiesBothGroups = TypeBitSet.Empty.With(TypeIndex<Gamma>.Value).With(TypeIndex<Alpha>.Value);

        filter.Matches(satisfiesOnlyFirstGroup).Should().BeFalse();
        filter.Matches(satisfiesBothGroups).Should().BeTrue();
    }

    [Fact]
    public void CombinedHasWithoutAny_AllMustHold()
    {
        var filter = ArchetypeFilter.Empty.Has<Alpha>().Without<Delta>().Any<Gamma, Delta>();
        var violatesWithout = TypeBitSet.Empty
            .With(TypeIndex<Alpha>.Value).With(TypeIndex<Delta>.Value);
        var satisfiesAll = TypeBitSet.Empty
            .With(TypeIndex<Alpha>.Value).With(TypeIndex<Gamma>.Value);

        filter.Matches(violatesWithout).Should().BeFalse();
        filter.Matches(satisfiesAll).Should().BeTrue();
    }

    [Fact]
    public void Combine_UnionsRequiredAndExcluded_ConcatenatesAnyGroups()
    {
        // a: requires Alpha, plus "Gamma or Delta". b: excludes Epsilon, plus "Alpha or Epsilon".
        // Combined: requires Alpha, excludes Epsilon, AND both "Gamma or Delta" and "Alpha or Epsilon" must hold.
        var a = ArchetypeFilter.Empty.Has<Alpha>().Any<Gamma, Delta>();
        var b = ArchetypeFilter.Empty.Without<Epsilon>().Any<Alpha, Epsilon>();
        var combined = a.Combine(b);

        var satisfiesAll = TypeBitSet.Empty.With(TypeIndex<Alpha>.Value).With(TypeIndex<Gamma>.Value);
        var missingRequiredAlpha = TypeBitSet.Empty.With(TypeIndex<Gamma>.Value);
        var violatesExcludedEpsilon = TypeBitSet.Empty.With(TypeIndex<Alpha>.Value).With(TypeIndex<Gamma>.Value).With(TypeIndex<Epsilon>.Value);

        combined.Matches(satisfiesAll).Should().BeTrue();
        combined.Matches(missingRequiredAlpha).Should().BeFalse();
        combined.Matches(violatesExcludedEpsilon).Should().BeFalse();
    }

    [Fact]
    public void Equals_ComparesByValue()
    {
        var a = ArchetypeFilter.Empty.Has<Alpha>().Without<Delta>();
        var b = ArchetypeFilter.Empty.Has<Alpha>().Without<Delta>();

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_IgnoresConstructionOrder()
    {
        // The cached identity hash must depend on final state, not builder history.
        var a = ArchetypeFilter.Empty.Has<Alpha>().Without<Delta>();
        var b = ArchetypeFilter.Empty.Without<Delta>().Has<Alpha>();

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Combine_EqualsHandBuiltEquivalent()
    {
        var left = ArchetypeFilter.Empty.Has<Alpha>();
        var right = ArchetypeFilter.Empty.Any<Gamma, Delta>();
        var combined = left.Combine(right);

        var handBuilt = ArchetypeFilter.Empty.Has<Alpha>().Any<Gamma, Delta>();
        combined.Equals(handBuilt).Should().BeTrue();
        combined.GetHashCode().Should().Be(handBuilt.GetHashCode());

        // Group order remains significant when both sides carry groups.
        var oneWay = ArchetypeFilter.Empty.Any<Gamma, Delta>().Any<Epsilon, Gamma>();
        var otherWay = ArchetypeFilter.Empty.Any<Epsilon, Gamma>().Any<Gamma, Delta>();
        oneWay.Equals(otherWay).Should().BeFalse();
    }
}
