using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests.Internal;

public class ArchetypeTests
{
    private struct Value : IComponent
    {
        public int Number;
    }

    [Fact]
    public void AddRow_ReturnsSequentialRowIndices()
    {
        var archetype = new Archetype(TypeBitSet.Empty, 4);

        archetype.AddRow(new Entity(1, 0)).Should().Be(0);
        archetype.AddRow(new Entity(2, 0)).Should().Be(1);
        archetype.Count.Should().Be(2);
    }

    [Fact]
    public void AddRow_RecordsTheEntityAtThatRow()
    {
        var archetype = new Archetype(TypeBitSet.Empty, 4);

        var row = archetype.AddRow(new Entity(7, 0));

        archetype.Entities[row].Should().Be(new Entity(7, 0));
    }

    [Fact]
    public void AddRow_GrowsPastInitialCapacity()
    {
        var archetype = new Archetype(TypeBitSet.Empty, 4);

        for (var i = 0; i < 200; i++)
            archetype.AddRow(new Entity(i + 1, 0));

        archetype.Count.Should().Be(200);
        archetype.Entities[199].Should().Be(new Entity(200, 0));
    }

    [Fact]
    public void AddRows_ReturnsTheStartingRow()
    {
        var archetype = new Archetype(TypeBitSet.Empty, 4);
        archetype.AddRow(new Entity(1, 0));

        var startRow = archetype.AddRows([new Entity(2, 0), new Entity(3, 0)]);

        startRow.Should().Be(1);
    }

    [Fact]
    public void AddRows_RecordsEveryEntityAtItsSequentialRow()
    {
        var archetype = new Archetype(TypeBitSet.Empty, 4);

        var startRow = archetype.AddRows([new Entity(10, 0), new Entity(20, 0), new Entity(30, 0)]);

        archetype.Entities[startRow].Should().Be(new Entity(10, 0));
        archetype.Entities[startRow + 1].Should().Be(new Entity(20, 0));
        archetype.Entities[startRow + 2].Should().Be(new Entity(30, 0));
    }

    [Fact]
    public void AddRows_IncrementsCountByTheBatchSize()
    {
        var archetype = new Archetype(TypeBitSet.Empty, 4);

        archetype.AddRows([new Entity(1, 0), new Entity(2, 0), new Entity(3, 0)]);

        archetype.Count.Should().Be(3);
    }

    [Fact]
    public void AddRows_GrowsPastInitialCapacityInOneCall()
    {
        var archetype = new Archetype(TypeBitSet.Empty, 4);
        var batch = Enumerable.Range(1, 200).Select(i => new Entity(i, 0)).ToArray();

        archetype.AddRows(batch);

        archetype.Count.Should().Be(200);
        archetype.Entities[199].Should().Be(new Entity(200, 0));
    }

    [Fact]
    public void RemoveRow_LastRow_ReturnsNull_AndDecrementsCount()
    {
        var archetype = new Archetype(TypeBitSet.Empty, 4);
        archetype.AddRow(new Entity(1, 0));

        var moved = archetype.RemoveRow(0);

        moved.IsNull.Should().BeTrue();
        archetype.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveRow_MiddleRow_ReturnsTheMovedEntity_AndSwapsItIn()
    {
        var archetype = new Archetype(TypeBitSet.Empty, 4);
        archetype.AddRow(new Entity(1, 0));
        archetype.AddRow(new Entity(2, 0));
        archetype.AddRow(new Entity(3, 0));

        var moved = archetype.RemoveRow(0);

        moved.Should().Be(new Entity(3, 0));
        archetype.Entities[0].Should().Be(new Entity(3, 0));
        archetype.Count.Should().Be(2);
    }

    [Fact]
    public void RemoveRow_AlsoSwapRemovesEveryComponentStorage()
    {
        var archetype = new Archetype(TypeBitSet.Empty.With(TypeIndex<Value>.Value), 4);
        var storage = archetype.GetOrCreateStorage<Value>();
        var rowA = archetype.AddRow(new Entity(1, 0));
        storage[rowA].Number = 11;
        var rowB = archetype.AddRow(new Entity(2, 0));
        storage[rowB].Number = 22;

        archetype.RemoveRow(rowA);

        storage[0].Number.Should().Be(22);
    }

    [Fact]
    public void GetOrCreateStorage_ReturnsTheSameInstanceOnRepeatedCalls()
    {
        var archetype = new Archetype(TypeBitSet.Empty, 4);

        var first = archetype.GetOrCreateStorage<Value>();
        var second = archetype.GetOrCreateStorage<Value>();

        first.Should().BeSameAs(second);
    }
}
