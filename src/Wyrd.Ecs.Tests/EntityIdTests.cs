namespace Wyrd.Ecs.Tests;

public class EntityIdTests
{
    [Fact]
    public void NewId_IsNotDefault()
    {
        EntityId.NewId().Should().NotBe(default(EntityId));
    }

    [Fact]
    public void NewId_ProducesUniqueValuesAcrossManyCalls()
    {
        var seen = new HashSet<UInt128>();

        for (var i = 0; i < 100_000; i++)
        {
            seen.Add(EntityId.NewId().Value).Should().BeTrue("each generated id must be unique");
        }
    }

    [Fact]
    public void SameValue_AreEqual()
    {
        var id = EntityId.NewId();

        new EntityId(id.Value).Should().Be(id);
    }
}
