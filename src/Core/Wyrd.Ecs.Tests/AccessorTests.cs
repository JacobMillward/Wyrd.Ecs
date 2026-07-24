namespace Wyrd.Ecs.Tests;

public class AccessorTests
{
    private struct Value : IComponent
    {
        public int Number;
    }

    private static Mut<Value> CreateMutChunk(Value[] items, int[] lastMarkedTick, int tick, int start, int length, bool tracked = true) =>
        Mut<Value>.CreateChunk(items, lastMarkedTick, tick, start, length, tracked);

    private static Ref<Value> CreateRefChunk(Value[] items, int[] lastMarkedTick, int tick, int start, int length, bool tracked = true) =>
        Ref<Value>.CreateChunk(items, lastMarkedTick, tick, start, length, tracked);

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

        var chunk = CreateMutChunk(items, lastMarkedTick, tick: 1, start: 2, length: 3);

        chunk.Length.Should().Be(3);
    }

    [Fact]
    public void Mut_Indexer_ReadsAndWritesTheUnderlyingArray()
    {
        var items = new Value[4];
        var lastMarkedTick = new int[4];
        var chunk = CreateMutChunk(items, lastMarkedTick, tick: 1, start: 0, length: 4);

        chunk[1].Number = 77;

        items[1].Number.Should().Be(77);
    }

    [Fact]
    public void Mut_Indexer_RespectsTheStartOffset()
    {
        var items = new Value[4];
        var lastMarkedTick = new int[4];
        var chunk = CreateMutChunk(items, lastMarkedTick, tick: 1, start: 2, length: 2);

        chunk[0].Number = 5;

        items[2].Number.Should().Be(5);
    }

    [Fact]
    public void Mut_Indexer_MarksExactlyThatRowDirty()
    {
        var items = new Value[3];
        var lastMarkedTick = new int[3];
        var chunk = CreateMutChunk(items, lastMarkedTick, tick: 1, start: 0, length: 3);

        _ = chunk[1];

        lastMarkedTick.Should().Equal(0, 1, 0);
    }

    [Fact]
    public void Mut_Indexer_RespectsStartOffsetWhenMarkingDirty()
    {
        var items = new Value[4];
        var lastMarkedTick = new int[4];
        var chunk = CreateMutChunk(items, lastMarkedTick, tick: 3, start: 2, length: 2);

        _ = chunk[0];

        lastMarkedTick.Should().Equal(0, 0, 3, 0);
    }

    [Fact]
    public void Mut_Indexer_WhenNotTracked_NeverMarksDirty()
    {
        var items = new Value[3];
        var lastMarkedTick = new int[3];
        var chunk = CreateMutChunk(items, lastMarkedTick, tick: 1, start: 0, length: 3, tracked: false);

        _ = chunk[1];

        lastMarkedTick.Should().Equal(0, 0, 0);
    }

    [Fact]
    public void Ref_Indexer_NeverMarksDirty()
    {
        var items = new Value[3] { new() { Number = 1 }, new() { Number = 2 }, new() { Number = 3 } };
        var lastMarkedTick = new int[3];
        var chunk = CreateRefChunk(items, lastMarkedTick, tick: 1, start: 0, length: 3);

        for (var i = 0; i < chunk.Length; i++)
            _ = chunk[i].Number;

        lastMarkedTick.Should().Equal(0, 0, 0);
    }

    [Fact]
    public void Ref_Indexer_ReadsTheUnderlyingArray()
    {
        var items = new Value[1] { new() { Number = 9 } };
        var lastMarkedTick = new int[1];
        var chunk = CreateRefChunk(items, lastMarkedTick, tick: 1, start: 0, length: 1);

        chunk[0].Number.Should().Be(9);
    }
}
