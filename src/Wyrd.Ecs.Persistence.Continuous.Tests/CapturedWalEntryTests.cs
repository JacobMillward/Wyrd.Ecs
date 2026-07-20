namespace Wyrd.Ecs.Persistence.Continuous.Tests;

public class CapturedWalEntryTests
{
    [Fact]
    public void Equals_ForTwoInstancesWithEqualButDistinctPayloadArrays_ReturnsTrue()
    {
        var entityId = EntityId.NewId();
        var first = new CapturedWalEntry(WalRecordKind.ComponentChanged, 5, entityId, "Position", 42u, [1, 2, 3]);
        var second = new CapturedWalEntry(WalRecordKind.ComponentChanged, 5, entityId, "Position", 42u, [1, 2, 3]);

        first.Equals(second).Should().BeTrue();
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Equals_ForDifferentPayloadContent_ReturnsFalse()
    {
        var entityId = EntityId.NewId();
        var first = new CapturedWalEntry(WalRecordKind.ComponentChanged, 5, entityId, "Position", 42u, [1, 2, 3]);
        var second = new CapturedWalEntry(WalRecordKind.ComponentChanged, 5, entityId, "Position", 42u, [1, 2, 4]);

        first.Equals(second).Should().BeFalse();
        (first == second).Should().BeFalse();
    }

    [Fact]
    public void Equals_ForDifferingNonPayloadFields_ReturnsFalse()
    {
        var entityId = EntityId.NewId();
        var baseline = new CapturedWalEntry(WalRecordKind.ComponentChanged, 5, entityId, "Position", 42u, [1, 2, 3]);

        baseline.Equals(baseline with { Kind = WalRecordKind.ComponentRemoved }).Should().BeFalse();
        baseline.Equals(baseline with { Tick = 6 }).Should().BeFalse();
        baseline.Equals(baseline with { EntityId = EntityId.NewId() }).Should().BeFalse();
        baseline.Equals(baseline with { Discriminator = "Velocity" }).Should().BeFalse();
        baseline.Equals(baseline with { SchemaHash = 43u }).Should().BeFalse();
        baseline.Equals(baseline with { SchemaHash = null }).Should().BeFalse();
    }
}
