namespace Wyrd.Ecs.Tests;

public class ChangeKindTests
{
    /// <summary>
    /// Every defined <see cref="ChangeKind"/> member, via reflection rather than a
    /// hand-maintained list — stays correct as members are added or removed, instead of
    /// silently under-checking a stale list.
    /// </summary>
    private static IEnumerable<ChangeKind> AllDefinedKinds => Enum.GetValues<ChangeKind>();

    [Fact]
    public void EveryDefinedKind_IsExactlyOneBit()
    {
        foreach (var kind in AllDefinedKinds)
            System.Numerics.BitOperations.PopCount((ushort)kind).Should().Be(1, $"{kind} should occupy exactly one bit");
    }

    [Fact]
    public void EveryPairOfDistinctKinds_SharesNoBits()
    {
        var kinds = AllDefinedKinds.ToArray();

        for (var i = 0; i < kinds.Length; i++)
        for (var j = i + 1; j < kinds.Length; j++)
            (kinds[i] & kinds[j]).Should().Be((ChangeKind)0, $"{kinds[i]} and {kinds[j]} should not overlap");
    }

    [Fact]
    public void CombiningTwoKinds_HasFlagReturnsTrueForBoth()
    {
        var combined = ChangeKind.TagAdded | ChangeKind.TagRemoved;

        combined.HasFlag(ChangeKind.TagAdded).Should().BeTrue();
        combined.HasFlag(ChangeKind.TagRemoved).Should().BeTrue();
        combined.HasFlag(ChangeKind.ValueChanged).Should().BeFalse();
    }
}
