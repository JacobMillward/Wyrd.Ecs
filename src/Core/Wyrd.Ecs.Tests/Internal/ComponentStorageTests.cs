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
        storage.RawLastMarkedTick.Length.Should().Be(storage.RawItems.Length);
    }

    [Fact]
    public void NewStorage_LastMarkedTickStartsAtZeroForEveryRow()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(4);

        storage.RawLastMarkedTick.Should().OnlyContain(tick => tick == 0);
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
        storage.RawLastMarkedTick.Length.Should().Be(storage.RawItems.Length);
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
    public void SwapRemove_MovesLastMarkedTickAlongsideTheValue()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(3);
        storage.MarkDirty(row: 2, tick: 9);

        storage.SwapRemove(row: 0, lastRow: 2);

        storage.RawLastMarkedTick[0].Should().Be(9);
        storage.RawLastMarkedTick[2].Should().Be(0);
    }

    [Fact]
    public void CopyRowTo_CopiesBothTheValueAndTheLastMarkedTick()
    {
        var source = new ComponentStorage<Value>();
        var destination = new ComponentStorage<Value>();
        source.EnsureCapacity(1);
        destination.EnsureCapacity(1);
        source[0].Number = 55;
        source.MarkDirty(row: 0, tick: 9);

        IComponentStorage sourceStorage = source;
        sourceStorage.CopyRowTo(0, destination, 0);

        destination[0].Number.Should().Be(55);
        destination.RawLastMarkedTick[0].Should().Be(9);
    }

    [Fact]
    public void CreateEmpty_ReturnsFreshStorageOfSameType()
    {
        IComponentStorage source = new ComponentStorage<Value>();

        var created = source.CreateEmpty(4);

        created.Should().BeOfType<ComponentStorage<Value>>();
    }

    [Fact]
    public void CreateEmpty_SizesTheStorageToTheRequestedCapacity()
    {
        IComponentStorage source = new ComponentStorage<Value>();

        var created = source.CreateEmpty(16);

        created.RawItems.Length.Should().Be(16);
        created.RawLastMarkedTick.Length.Should().Be(16);
    }

    [Fact]
    public void MarkDirty_SetsTheLastMarkedTickForThatRow()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(1);

        storage.MarkDirty(row: 0, tick: 5);

        storage.RawLastMarkedTick[0].Should().Be(5);
    }

    [Fact]
    public void MarkDirty_ANewerTick_OverwritesTheOlderOne()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(1);

        storage.MarkDirty(row: 0, tick: 5);
        storage.MarkDirty(row: 0, tick: 6);

        storage.RawLastMarkedTick[0].Should().Be(6);
    }

    [Fact]
    public void Fill_WritesTheSameValueToEveryRowInTheRange()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(5);

        storage.Fill(startRow: 1, count: 3, new Value { Number = 7 });

        storage[0].Number.Should().Be(0);
        storage[1].Number.Should().Be(7);
        storage[2].Number.Should().Be(7);
        storage[3].Number.Should().Be(7);
        storage[4].Number.Should().Be(0);
    }

    [Fact]
    public void Fill_WritesIndependentCopiesNotASharedReference()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(2);

        storage.Fill(startRow: 0, count: 2, new Value { Number = 1 });
        storage[0].Number = 99;

        storage[1].Number.Should().Be(1); // mutating one row's copy must not affect the other
    }

    [Fact]
    public void MarkDirtyRange_StampsEveryRowInTheRangeWithTheGivenTick()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(4);

        storage.MarkDirtyRange(startRow: 1, count: 2, tick: 42);

        storage.RawLastMarkedTick[0].Should().Be(0);
        storage.RawLastMarkedTick[1].Should().Be(42);
        storage.RawLastMarkedTick[2].Should().Be(42);
        storage.RawLastMarkedTick[3].Should().Be(0);
    }
}
