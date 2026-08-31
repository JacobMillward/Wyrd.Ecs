using System.Collections.Immutable;
using Wyrd.Ecs.Generators;

namespace Wyrd.Ecs.Generators.Tests;

public class SequenceEqualComparerTests
{
    [Fact]
    public void Equals_TwoSeparatelyBuiltArraysWithEqualElements_ReturnsTrue()
    {
        var first = new[] { 1, 2, 3 }.ToImmutableArray();
        var second = new[] { 1, 2, 3 }.ToImmutableArray();

        first.Equals(second).Should().BeFalse();
        SequenceEqualComparer<int>.Instance.Equals(first, second).Should().BeTrue();
    }

    [Fact]
    public void Equals_ArraysWithDifferentElements_ReturnsFalse()
    {
        var first = new[] { 1, 2, 3 }.ToImmutableArray();
        var second = new[] { 1, 2, 4 }.ToImmutableArray();

        SequenceEqualComparer<int>.Instance.Equals(first, second).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_TwoSeparatelyBuiltArraysWithEqualElements_ReturnsSameValue()
    {
        var first = new[] { "a", "b" }.ToImmutableArray();
        var second = new[] { "a", "b" }.ToImmutableArray();

        SequenceEqualComparer<string>.Instance.GetHashCode(first).Should().Be(SequenceEqualComparer<string>.Instance.GetHashCode(second));
    }
}
