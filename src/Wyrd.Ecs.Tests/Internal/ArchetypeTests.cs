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
        var archetype = new Archetype(ArchetypeSignature.Empty, 4);

        archetype.AddRow(new Entity(1, 0)).Should().Be(0);
        archetype.AddRow(new Entity(2, 0)).Should().Be(1);
        archetype.Count.Should().Be(2);
    }

    [Fact]
    public void AddRow_RecordsTheEntityAtThatRow()
    {
        var archetype = new Archetype(ArchetypeSignature.Empty, 4);

        var row = archetype.AddRow(new Entity(7, 0));

        archetype.Entities[row].Should().Be(new Entity(7, 0));
    }

    [Fact]
    public void AddRow_GrowsPastInitialCapacity()
    {
        var archetype = new Archetype(ArchetypeSignature.Empty, 4);

        for (var i = 0; i < 200; i++)
            archetype.AddRow(new Entity(i + 1, 0));

        archetype.Count.Should().Be(200);
        archetype.Entities[199].Should().Be(new Entity(200, 0));
    }

    [Fact]
    public void RemoveRow_LastRow_ReturnsNull_AndDecrementsCount()
    {
        var archetype = new Archetype(ArchetypeSignature.Empty, 4);
        archetype.AddRow(new Entity(1, 0));

        var moved = archetype.RemoveRow(0);

        moved.IsNull.Should().BeTrue();
        archetype.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveRow_MiddleRow_ReturnsTheMovedEntity_AndSwapsItIn()
    {
        var archetype = new Archetype(ArchetypeSignature.Empty, 4);
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
        var archetype = new Archetype(ArchetypeSignature.Empty.With(TypeIndex<Value>.Value), 4);
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
        var archetype = new Archetype(ArchetypeSignature.Empty, 4);

        var first = archetype.GetOrCreateStorage<Value>();
        var second = archetype.GetOrCreateStorage<Value>();

        first.Should().BeSameAs(second);
    }
}
