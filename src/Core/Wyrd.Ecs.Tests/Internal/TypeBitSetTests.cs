using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests.Internal;

public class TypeBitSetTests
{
    [Fact]
    public void Empty_ContainsNothing()
    {
        TypeBitSet.Empty.Contains(0).Should().BeFalse();
        TypeBitSet.Empty.Contains(200).Should().BeFalse();
    }

    [Fact]
    public void Default_BehavesIdenticallyToEmpty()
    {
        var defaulted = default(TypeBitSet);

        defaulted.Contains(0).Should().BeFalse();
        defaulted.Contains(300).Should().BeFalse();
        defaulted.Should().Be(TypeBitSet.Empty);
        defaulted.GetHashCode().Should().Be(TypeBitSet.Empty.GetHashCode());
        defaulted.IsSubsetOf(TypeBitSet.Empty.With(1)).Should().BeTrue();
        defaulted.Intersects(TypeBitSet.Empty.With(1)).Should().BeFalse();
    }

    [Fact]
    public void With_IndexPastInlineCapacity_StillWorks()
    {
        // 4 inline 64-bit words = 256 bits; 300 forces the heap-array overflow path.
        var signature = TypeBitSet.Empty.With(300);

        signature.Contains(300).Should().BeTrue();
        signature.Contains(299).Should().BeFalse();
    }

    [Fact]
    public void SameBits_AreEqual_AcrossInlineAndOverflowConstructionOrder()
    {
        var a = TypeBitSet.Empty.With(1).With(300);
        var b = TypeBitSet.Empty.With(300).With(1);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void With_AddsTheBit()
    {
        var signature = TypeBitSet.Empty.With(5);

        signature.Contains(5).Should().BeTrue();
        signature.Contains(6).Should().BeFalse();
    }

    [Fact]
    public void With_HighBitIndex_StillWorks()
    {
        var signature = TypeBitSet.Empty.With(130);

        signature.Contains(130).Should().BeTrue();
        signature.Contains(129).Should().BeFalse();
    }

    [Fact]
    public void Without_RemovesTheBit()
    {
        var signature = TypeBitSet.Empty.With(5).With(9).Without(5);

        signature.Contains(5).Should().BeFalse();
        signature.Contains(9).Should().BeTrue();
    }

    [Fact]
    public void Without_MissingBit_IsANoOp()
    {
        var signature = TypeBitSet.Empty.With(3);

        signature.Without(200).Contains(3).Should().BeTrue();
    }

    [Fact]
    public void SameBits_AreEqual_EvenWithDifferentConstructionOrder()
    {
        var a = TypeBitSet.Empty.With(1).With(70);
        var b = TypeBitSet.Empty.With(70).With(1);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void SameBits_AreEqual_EvenWhenOneHasTrailingZeroPadding()
    {
        var a = TypeBitSet.Empty.With(1);
        var b = TypeBitSet.Empty.With(1).With(200).Without(200);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void DifferentBits_AreNotEqual()
    {
        TypeBitSet.Empty.With(1).Should().NotBe(TypeBitSet.Empty.With(2));
    }

    [Fact]
    public void UsableAsDictionaryKey()
    {
        var map = new Dictionary<TypeBitSet, string>
        {
            [TypeBitSet.Empty.With(1).With(2)] = "found"
        };

        map[TypeBitSet.Empty.With(2).With(1)].Should().Be("found");
    }

    [Fact]
    public void Intersects_ReturnsTrueWhenAnyBitShared()
    {
        var a = TypeBitSet.Empty.With(1).With(3);
        var b = TypeBitSet.Empty.With(3).With(5);

        a.Intersects(b).Should().BeTrue();
    }

    [Fact]
    public void Intersects_ReturnsFalseWhenNoBitsShared()
    {
        var a = TypeBitSet.Empty.With(1).With(2);
        var b = TypeBitSet.Empty.With(3).With(4);

        a.Intersects(b).Should().BeFalse();
    }

    [Fact]
    public void Intersects_ReturnsFalseAgainstEmpty()
    {
        var a = TypeBitSet.Empty.With(1);

        a.Intersects(TypeBitSet.Empty).Should().BeFalse();
    }

    [Fact]
    public void Intersects_HandlesDifferentWordLengths()
    {
        var a = TypeBitSet.Empty.With(1);
        var b = TypeBitSet.Empty.With(1).With(200);

        a.Intersects(b).Should().BeTrue();
    }

    [Fact]
    public void SetBits_OnEmpty_YieldsNothing()
    {
        var indices = new List<int>();
        foreach (var index in TypeBitSet.Empty.SetBits) indices.Add(index);

        indices.Should().BeEmpty();
    }

    [Fact]
    public void SetBits_WithSeveralInlineBitsSet_YieldsThemAllInAscendingOrder()
    {
        var signature = TypeBitSet.Empty.With(3).With(0).With(63).With(64);

        var indices = new List<int>();
        foreach (var index in signature.SetBits) indices.Add(index);

        indices.Should().Equal(0, 3, 63, 64);
    }

    [Fact]
    public void SetBits_WithABitPastInlineCapacity_StillYieldsIt()
    {
        // 4 inline 64-bit words = 256 bits; 300 forces the heap-array overflow path.
        var signature = TypeBitSet.Empty.With(5).With(300);

        var indices = new List<int>();
        foreach (var index in signature.SetBits) indices.Add(index);

        indices.Should().Equal(5, 300);
    }
}
