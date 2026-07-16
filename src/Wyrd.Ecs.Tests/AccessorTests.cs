namespace Wyrd.Ecs.Tests;

public class AccessorTests
{
    private struct Value : IComponent
    {
        public int Number;
    }

    private static Mut<Value> CreateMutChunk(Value[] items, bool[] dirty, int start, int length) =>
        Mut<Value>.CreateChunk(items, dirty, start, length);

    private static Ref<Value> CreateRefChunk(Value[] items, bool[] dirty, int start, int length) =>
        Ref<Value>.CreateChunk(items, dirty, start, length);

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
        var dirty = new bool[10];

        var chunk = CreateMutChunk(items, dirty, start: 2, length: 3);

        chunk.Length.Should().Be(3);
    }

    [Fact]
    public void Mut_Indexer_ReadsAndWritesTheUnderlyingArray()
    {
        var items = new Value[4];
        var dirty = new bool[4];
        var chunk = CreateMutChunk(items, dirty, start: 0, length: 4);

        chunk[1].Number = 77;

        items[1].Number.Should().Be(77);
    }

    [Fact]
    public void Mut_Indexer_RespectsTheStartOffset()
    {
        var items = new Value[4];
        var dirty = new bool[4];
        var chunk = CreateMutChunk(items, dirty, start: 2, length: 2);

        chunk[0].Number = 5;

        items[2].Number.Should().Be(5);
    }

    [Fact]
    public void Mut_Indexer_MarksExactlyThatRowDirty()
    {
        var items = new Value[3];
        var dirty = new bool[3];
        var chunk = CreateMutChunk(items, dirty, start: 0, length: 3);

        _ = chunk[1];

        dirty.Should().Equal(false, true, false);
    }

    [Fact]
    public void Ref_Indexer_NeverMarksDirty()
    {
        var items = new Value[3] { new() { Number = 1 }, new() { Number = 2 }, new() { Number = 3 } };
        var dirty = new bool[3];
        var chunk = CreateRefChunk(items, dirty, start: 0, length: 3);

        for (var i = 0; i < chunk.Length; i++)
            _ = chunk[i].Number;

        dirty.Should().Equal(false, false, false);
    }

    [Fact]
    public void Ref_Indexer_ReadsTheUnderlyingArray()
    {
        var items = new Value[1] { new() { Number = 9 } };
        var dirty = new bool[1];
        var chunk = CreateRefChunk(items, dirty, start: 0, length: 1);

        chunk[0].Number.Should().Be(9);
    }
}
