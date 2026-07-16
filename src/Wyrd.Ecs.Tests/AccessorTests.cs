namespace Wyrd.Ecs.Tests;

public class AccessorTests
{
    private struct Value : IComponent
    {
        public int Number;
    }

    private static Mut<Value> CreateMutChunk(Value[] items, int[] lastMarkedTick, int tick, DirtyLog dirtyLog, int start, int length) =>
        Mut<Value>.CreateChunk(items, lastMarkedTick, tick, dirtyLog, start, length);

    private static Ref<Value> CreateRefChunk(Value[] items, int[] lastMarkedTick, int tick, DirtyLog dirtyLog, int start, int length) =>
        Ref<Value>.CreateChunk(items, lastMarkedTick, tick, dirtyLog, start, length);

    private static DirtyLog CreateEmptyDirtyLog(Entity[] archetypeEntities) =>
        new(archetypeEntities, new DirtyEntry[archetypeEntities.Length + 1], 0);

    [Fact]
    public void Mut_TypeIndex_MatchesTypeIndexOfT()
    {
        Mut<Value>.TypeIndex.Should().Be(Wyrd.Ecs.Internal.TypeIndex<Value>.Value);
    }

    [Fact]
    public void Ref_TypeIndex_MatchesTypeIndexOfT()
    {
        Ref<Value>.TypeIndex.Should().Be(Wyrd.Ecs.Internal.TypeIndex<Value>.Value);
    }

    [Fact]
    public void Mut_Length_MatchesRequestedSlice()
    {
        var items = new Value[10];
        var lastMarkedTick = new int[10];
        var dirtyLog = CreateEmptyDirtyLog(new Entity[10]);

        var chunk = CreateMutChunk(items, lastMarkedTick, tick: 1, dirtyLog, start: 2, length: 3);

        chunk.Length.Should().Be(3);
    }

    [Fact]
    public void Mut_Indexer_ReadsAndWritesTheUnderlyingArray()
    {
        var items = new Value[4];
        var lastMarkedTick = new int[4];
        var dirtyLog = CreateEmptyDirtyLog(new Entity[4]);
        var chunk = CreateMutChunk(items, lastMarkedTick, tick: 1, dirtyLog, start: 0, length: 4);

        chunk[1].Number = 77;

        items[1].Number.Should().Be(77);
    }

    [Fact]
    public void Mut_Indexer_RespectsTheStartOffset()
    {
        var items = new Value[4];
        var lastMarkedTick = new int[4];
        var dirtyLog = CreateEmptyDirtyLog(new Entity[4]);
        var chunk = CreateMutChunk(items, lastMarkedTick, tick: 1, dirtyLog, start: 2, length: 2);

        chunk[0].Number = 5;

        items[2].Number.Should().Be(5);
    }

    [Fact]
    public void Mut_Indexer_MarksExactlyThatRowDirty()
    {
        var items = new Value[3];
        var lastMarkedTick = new int[3];
        var dirtyLog = CreateEmptyDirtyLog(new Entity[3]);
        var chunk = CreateMutChunk(items, lastMarkedTick, tick: 1, dirtyLog, start: 0, length: 3);

        _ = chunk[1];

        lastMarkedTick.Should().Equal(0, 1, 0);
    }

    [Fact]
    public void Mut_Indexer_AppendsTheTouchedEntityToTheDirtyLog()
    {
        var items = new Value[3];
        var lastMarkedTick = new int[3];
        var entities = new[] { new Entity(1, 0), new Entity(2, 0), new Entity(3, 0) };
        var dirtyLog = CreateEmptyDirtyLog(entities);
        var chunk = CreateMutChunk(items, lastMarkedTick, tick: 7, dirtyLog, start: 0, length: 3);

        _ = chunk[1];

        dirtyLog.Count.Should().Be(1);
        dirtyLog.Entries[0].Should().Be(new DirtyEntry(entities[1], 7));
    }

    [Fact]
    public void Mut_Indexer_RespectsStartOffsetWhenResolvingTheTouchedEntity()
    {
        var items = new Value[4];
        var lastMarkedTick = new int[4];
        var entities = new[] { new Entity(1, 0), new Entity(2, 0), new Entity(3, 0), new Entity(4, 0) };
        var dirtyLog = CreateEmptyDirtyLog(entities);
        var chunk = CreateMutChunk(items, lastMarkedTick, tick: 3, dirtyLog, start: 2, length: 2);

        _ = chunk[0];

        dirtyLog.Entries[0].Entity.Should().Be(entities[2]);
    }

    [Fact]
    public void Mut_Indexer_TouchedTwiceSameTick_AppendsOnce()
    {
        var items = new Value[3];
        var lastMarkedTick = new int[3];
        var dirtyLog = CreateEmptyDirtyLog(new Entity[3]);
        var chunk = CreateMutChunk(items, lastMarkedTick, tick: 7, dirtyLog, start: 0, length: 3);

        _ = chunk[1];
        _ = chunk[1];

        dirtyLog.Count.Should().Be(1);
    }

    [Fact]
    public void Ref_Indexer_NeverMarksDirty()
    {
        var items = new Value[3] { new() { Number = 1 }, new() { Number = 2 }, new() { Number = 3 } };
        var lastMarkedTick = new int[3];
        var dirtyLog = CreateEmptyDirtyLog(new Entity[3]);
        var chunk = CreateRefChunk(items, lastMarkedTick, tick: 1, dirtyLog, start: 0, length: 3);

        for (var i = 0; i < chunk.Length; i++)
            _ = chunk[i].Number;

        lastMarkedTick.Should().Equal(0, 0, 0);
        dirtyLog.Count.Should().Be(0);
    }

    [Fact]
    public void Ref_Indexer_ReadsTheUnderlyingArray()
    {
        var items = new Value[1] { new() { Number = 9 } };
        var lastMarkedTick = new int[1];
        var dirtyLog = CreateEmptyDirtyLog(new Entity[1]);
        var chunk = CreateRefChunk(items, lastMarkedTick, tick: 1, dirtyLog, start: 0, length: 1);

        chunk[0].Number.Should().Be(9);
    }
}
