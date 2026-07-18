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
        var entity = new Entity(1, 0);
        storage.MarkDirty(row: 2, entity, tick: 9);

        storage.SwapRemove(row: 0, lastRow: 2);

        storage.RawLastMarkedTick[0].Should().Be(9);
        storage.RawLastMarkedTick[2].Should().Be(0);
    }

    [Fact]
    public void CreateEmpty_ReturnsFreshStorageOfSameType()
    {
        IComponentStorage source = new ComponentStorage<Value>();

        var created = source.CreateEmpty();

        created.Should().BeOfType<ComponentStorage<Value>>();
    }

    [Fact]
    public void CreateEmpty_HasItsOwnIndependentDirtyLog()
    {
        var source = new ComponentStorage<Value>();
        source.EnsureCapacity(1);
        source.MarkDirty(row: 0, new Entity(1, 0), tick: 5);

        var created = (ComponentStorage<Value>)((IComponentStorage)source).CreateEmpty();

        created.ReadDirtyLogSince(sinceTick: 0).Length.Should().Be(0);
    }

    [Fact]
    public void MarkDirty_FirstTouchThisTick_AppendsOneEntry()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(1);
        var entity = new Entity(1, 0);

        storage.MarkDirty(row: 0, entity, tick: 5);

        storage.RawLastMarkedTick[0].Should().Be(5);
        storage.ReadDirtyLogSince(sinceTick: 0).ToArray().Should().Equal(new DirtyEntry(entity, 5));
    }

    [Fact]
    public void MarkDirty_SecondTouchSameTick_DoesNotAppendAgain()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(1);
        var entity = new Entity(1, 0);

        storage.MarkDirty(row: 0, entity, tick: 5);
        storage.MarkDirty(row: 0, entity, tick: 5);

        storage.ReadDirtyLogSince(sinceTick: 0).Length.Should().Be(1);
    }

    [Fact]
    public void MarkDirty_TouchOnADifferentTick_AppendsAgain()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(1);
        var entity = new Entity(1, 0);

        storage.MarkDirty(row: 0, entity, tick: 5);
        storage.MarkDirty(row: 0, entity, tick: 6);

        storage.ReadDirtyLogSince(sinceTick: 0).ToArray().Should()
            .Equal(new DirtyEntry(entity, 5), new DirtyEntry(entity, 6));
    }

    [Fact]
    public void MarkDirty_GrowsTheLogPastItsInitialCapacity()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(1);
        var entity = new Entity(1, 0);

        for (var tick = 1; tick <= 20; tick++)
            storage.MarkDirty(row: 0, entity, tick);

        storage.ReadDirtyLogSince(sinceTick: 0).Length.Should().Be(20);
    }

    [Fact]
    public void ReadDirtyLogSince_ExcludesEntriesAtOrBeforeTheCursor()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(1);
        var entity = new Entity(1, 0);
        storage.MarkDirty(row: 0, entity, tick: 5);
        storage.MarkDirty(row: 0, entity, tick: 6);
        storage.MarkDirty(row: 0, entity, tick: 7);

        var entries = storage.ReadDirtyLogSince(sinceTick: 6);

        entries.ToArray().Should().Equal(new DirtyEntry(entity, 7));
    }

    [Fact]
    public void GetDirtyLogForChunk_ExposesTheCurrentArchetypeEntities()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(2);
        var entities = new[] { new Entity(1, 0), new Entity(2, 0) };

        var dirtyLog = storage.GetDirtyLogForChunk(entities, additionalCapacity: 2);

        dirtyLog.ArchetypeEntities.Should().BeSameAs(entities);
    }

    [Fact]
    public void GetDirtyLogForChunk_GrowsBackingArrayWhenNeeded()
    {
        var storage = new ComponentStorage<Value>();
        storage.EnsureCapacity(10);
        var entities = new Entity[10];

        var dirtyLog = storage.GetDirtyLogForChunk(entities, additionalCapacity: 10);

        dirtyLog.Entries.Length.Should().BeGreaterThanOrEqualTo(10);
    }
}
