namespace Wyrd.Ecs.Tests;

public class EntityTests
{
    [Fact]
    public void Default_IsNull()
    {
        default(Entity).IsNull.Should().BeTrue();
    }

    [Fact]
    public void Null_IsNull()
    {
        Entity.Null.IsNull.Should().BeTrue();
    }

    [Fact]
    public void NonZeroId_IsNotNull()
    {
        new Entity(1, 0).IsNull.Should().BeFalse();
    }

    [Fact]
    public void SameIdAndGeneration_AreEqual()
    {
        new Entity(5, 2).Should().Be(new Entity(5, 2));
    }

    [Fact]
    public void DifferentGeneration_AreNotEqual()
    {
        new Entity(5, 2).Should().NotBe(new Entity(5, 3));
    }
}
