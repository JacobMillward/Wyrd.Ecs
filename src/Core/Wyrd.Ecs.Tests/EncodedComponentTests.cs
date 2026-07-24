namespace Wyrd.Ecs.Tests;

public class EncodedComponentTests
{
    [Fact]
    public void Equals_ForTwoInstancesWithEqualButDistinctDataArrays_ReturnsTrue()
    {
        var entity = new Entity(1, 1);
        var first = new EncodedComponent(entity, "Position", 42u, [1, 2, 3]);
        var second = new EncodedComponent(entity, "Position", 42u, [1, 2, 3]);

        first.Equals(second).Should().BeTrue();
        (first == second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Equals_ForTwoInstancesWithDifferentDataContent_ReturnsFalse()
    {
        var entity = new Entity(1, 1);
        var first = new EncodedComponent(entity, "Position", 42u, [1, 2, 3]);
        var second = new EncodedComponent(entity, "Position", 42u, [1, 2, 4]);

        first.Equals(second).Should().BeFalse();
        (first == second).Should().BeFalse();
    }

    [Fact]
    public void Equals_ForTwoInstancesWithDifferentDataLength_ReturnsFalse()
    {
        var entity = new Entity(1, 1);
        var first = new EncodedComponent(entity, "Position", 42u, [1, 2, 3]);
        var second = new EncodedComponent(entity, "Position", 42u, [1, 2, 3, 4]);

        first.Equals(second).Should().BeFalse();
    }

    [Fact]
    public void Equals_ForDifferingNonDataFields_ReturnsFalse()
    {
        var entity = new Entity(1, 1);
        var baseline = new EncodedComponent(entity, "Position", 42u, [1, 2, 3]);

        baseline.Equals(baseline with { Entity = new Entity(2, 1) }).Should().BeFalse();
        baseline.Equals(baseline with { Discriminator = "Velocity" }).Should().BeFalse();
        baseline.Equals(baseline with { SchemaHash = 43u }).Should().BeFalse();
        baseline.Equals(baseline with { SchemaHash = null }).Should().BeFalse();
    }
}
