using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests.Internal;

public class EntityTableTests
{
    [Fact]
    public void Place_OutOfOrderWithinABatch_DoesNotPrematurelyExposeAnEarlierReservedIdAsAlive()
    {
        var table = new EntityTable();
        var archetype = new Archetype(ArchetypeSignature.Empty, 4);

        var a = table.Reserve();
        var b = table.Reserve();

        table.Place(b, archetype);

        table.IsAlive(a.Id, a.Generation).Should().BeFalse();
        table.IsAlive(b.Id, b.Generation).Should().BeTrue("b must be alive immediately from its own Place call, regardless of a's");

        table.Place(a, archetype);

        table.IsAlive(a.Id, a.Generation).Should().BeTrue();
        table.IsAlive(b.Id, b.Generation).Should().BeTrue();
    }

    [Fact]
    public void ReserveRange_FreshTable_ReturnsSequentialNewIds()
    {
        var table = new EntityTable();
        Span<Entity> batch = new Entity[3];

        table.ReserveRange(batch);

        batch[0].Id.Should().Be(1);
        batch[1].Id.Should().Be(2);
        batch[2].Id.Should().Be(3);
    }

    [Fact]
    public void ReserveRange_ReturnsDistinctIds()
    {
        var table = new EntityTable();
        Span<Entity> batch = new Entity[50];

        table.ReserveRange(batch);

        batch.ToArray().Select(e => e.Id).Distinct().Should().HaveCount(50);
    }

    [Fact]
    public void ReserveRange_ProducesTheSameIdsAsSequentialReserveCalls()
    {
        var rangeTable = new EntityTable();
        Span<Entity> batch = new Entity[10];
        rangeTable.ReserveRange(batch);

        var sequentialTable = new EntityTable();
        var sequential = new Entity[10];
        for (var i = 0; i < 10; i++) sequential[i] = sequentialTable.Reserve();

        batch.ToArray().Should().BeEquivalentTo(sequential, options => options.WithStrictOrdering());
    }

    [Fact]
    public void ReserveRange_UsesRecycledIdsBeforeMintingNewOnes()
    {
        var table = new EntityTable();
        var archetype = new Archetype(ArchetypeSignature.Empty, 4);
        var a = table.Reserve();
        var b = table.Reserve();
        table.Place(a, archetype);
        table.Place(b, archetype);
        table.Destroy(a.Id);
        table.Destroy(b.Id); // two ids now pending for recycling

        Span<Entity> batch = new Entity[5]; // more than the 2 recycled -> must mint 3 new ones too

        table.ReserveRange(batch);

        var ids = batch.ToArray().Select(e => e.Id).ToArray();
        ids.Distinct().Should().HaveCount(5);
        ids.Should().Contain([a.Id, b.Id]);
    }

    [Fact]
    public void PlaceBatch_MakesEveryEntityAliveAtItsAssignedRow()
    {
        var table = new EntityTable();
        var archetype = new Archetype(ArchetypeSignature.Empty, 4);
        var a = table.Reserve();
        var b = table.Reserve();
        var c = table.Reserve();

        table.PlaceBatch([a, b, c], archetype, startRow: 0);

        table.IsAlive(a.Id, a.Generation).Should().BeTrue();
        table.IsAlive(b.Id, b.Generation).Should().BeTrue();
        table.IsAlive(c.Id, c.Generation).Should().BeTrue();
    }

    [Fact]
    public void PlaceBatch_AssignsSequentialRowsStartingAtStartRow()
    {
        var table = new EntityTable();
        var archetype = new Archetype(ArchetypeSignature.Empty, 4);
        var a = table.Reserve();
        var b = table.Reserve();

        table.PlaceBatch([a, b], archetype, startRow: 5);

        table[a.Id].Row.Should().Be(5);
        table[b.Id].Row.Should().Be(6);
        table[a.Id].Archetype.Should().BeSameAs(archetype);
    }
}
