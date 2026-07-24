using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests.Internal;

public class ArchetypeSignatureTests
{
    [Fact]
    public void Empty_ContainsNothing()
    {
        ArchetypeSignature.Empty.Contains(0).Should().BeFalse();
        ArchetypeSignature.Empty.Contains(200).Should().BeFalse();
    }

    [Fact]
    public void With_AddsTheBit()
    {
        var signature = ArchetypeSignature.Empty.With(5);

        signature.Contains(5).Should().BeTrue();
        signature.Contains(6).Should().BeFalse();
    }

    [Fact]
    public void With_HighBitIndex_StillWorks()
    {
        var signature = ArchetypeSignature.Empty.With(130);

        signature.Contains(130).Should().BeTrue();
        signature.Contains(129).Should().BeFalse();
    }

    [Fact]
    public void Without_RemovesTheBit()
    {
        var signature = ArchetypeSignature.Empty.With(5).With(9).Without(5);

        signature.Contains(5).Should().BeFalse();
        signature.Contains(9).Should().BeTrue();
    }

    [Fact]
    public void Without_MissingBit_IsANoOp()
    {
        var signature = ArchetypeSignature.Empty.With(3);

        signature.Without(200).Contains(3).Should().BeTrue();
    }

    [Fact]
    public void SameBits_AreEqual_EvenWithDifferentConstructionOrder()
    {
        var a = ArchetypeSignature.Empty.With(1).With(70);
        var b = ArchetypeSignature.Empty.With(70).With(1);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void SameBits_AreEqual_EvenWhenOneHasTrailingZeroPadding()
    {
        var a = ArchetypeSignature.Empty.With(1);
        var b = ArchetypeSignature.Empty.With(1).With(200).Without(200);

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void DifferentBits_AreNotEqual()
    {
        ArchetypeSignature.Empty.With(1).Should().NotBe(ArchetypeSignature.Empty.With(2));
    }

    [Fact]
    public void UsableAsDictionaryKey()
    {
        var map = new Dictionary<ArchetypeSignature, string>
        {
            [ArchetypeSignature.Empty.With(1).With(2)] = "found"
        };

        map[ArchetypeSignature.Empty.With(2).With(1)].Should().Be("found");
    }

    [Fact]
    public void Intersects_ReturnsTrueWhenAnyBitShared()
    {
        var a = ArchetypeSignature.Empty.With(1).With(3);
        var b = ArchetypeSignature.Empty.With(3).With(5);

        a.Intersects(b).Should().BeTrue();
    }

    [Fact]
    public void Intersects_ReturnsFalseWhenNoBitsShared()
    {
        var a = ArchetypeSignature.Empty.With(1).With(2);
        var b = ArchetypeSignature.Empty.With(3).With(4);

        a.Intersects(b).Should().BeFalse();
    }

    [Fact]
    public void Intersects_ReturnsFalseAgainstEmpty()
    {
        var a = ArchetypeSignature.Empty.With(1);

        a.Intersects(ArchetypeSignature.Empty).Should().BeFalse();
    }

    [Fact]
    public void Intersects_HandlesDifferentWordLengths()
    {
        var a = ArchetypeSignature.Empty.With(1);
        var b = ArchetypeSignature.Empty.With(1).With(200);

        a.Intersects(b).Should().BeTrue();
    }
}
