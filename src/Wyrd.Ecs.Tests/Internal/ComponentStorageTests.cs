using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests.Internal;

public class ComponentStorageTests
{
    private struct Value : IComponent
    {
        public int Number;
    }

    [Fact]
    public void NewStorage_HasNonEmptyBackingArrays()
    {
        var storage = new ComponentStorage<Value>();

        storage.RawItems.Length.Should().BeGreaterThan(0);
        storage.RawDirty.Length.Should().Be(storage.RawItems.Length);
    }

    [Fact]
    public void IndexerReturnsWritableReference()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(1);

        storage[0].Number = 42;

        storage[0].Number.Should().Be(42);
    }

    [Fact]
    public void EnsureCapacity_GrowsBothArraysTogether()
    {
        var storage = new ComponentStorage<Value>();

        storage.EnsureCapacity(100);

        storage.RawItems.Length.Should().BeGreaterThanOrEqualTo(100);
        storage.RawDirty.Length.Should().Be(storage.RawItems.Length);
    }

    [Fact]
    public void EnsureCapacity_PreservesExistingValues()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(2);
        storage[0].Number = 7;

        storage.EnsureCapacity(500);

        storage[0].Number.Should().Be(7);
    }

    [Fact]
    public void SwapRemove_MiddleRow_MovesLastRowIntoIt()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(3);
        storage[0].Number = 1;
        storage[1].Number = 2;
        storage[2].Number = 3;

        storage.SwapRemove(row: 0, lastRow: 2);

        storage[0].Number.Should().Be(3);
    }

    [Fact]
    public void SwapRemove_LastRow_ClearsIt()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(2);
        storage[1].Number = 9;

        storage.SwapRemove(row: 1, lastRow: 1);

        storage[1].Number.Should().Be(0);
    }

    [Fact]
    public void CopyRowTo_CopiesTheValueOnly()
    {
        var source = new ComponentStorage<Value>();
        var destination = new ComponentStorage<Value>();
        source.EnsureCapacity(1);
        destination.EnsureCapacity(1);
        source[0].Number = 55;

        IComponentStorage sourceStorage = source;
        sourceStorage.CopyRowTo(0, destination, 0);

        destination[0].Number.Should().Be(55);
    }

    [Fact]
    public void CreateEmpty_ReturnsFreshStorageOfSameType()
    {
        IComponentStorage source = new ComponentStorage<Value>();

        var created = source.CreateEmpty();

        created.Should().BeOfType<ComponentStorage<Value>>();
    }
}
