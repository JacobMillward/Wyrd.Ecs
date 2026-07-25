namespace Wyrd.Ecs.Tests;

file struct Tag1 : ITag;
file struct Comp1 : IComponent { public int Value; }

public class QueryMarkerTests
{
    [Fact]
    public void HasWithoutAny_AcceptBothComponentAndTagTypes()
    {
        _ = typeof(Has<Tag1>);
        _ = typeof(Has<Comp1>);
        _ = typeof(Without<Tag1>);
        _ = typeof(Without<Comp1>);
        _ = typeof(Any<Tag1, Comp1>);
    }

    [Fact]
    public void WritesReads_AcceptComponentTypes()
    {
        _ = typeof(Writes<Comp1>);
        _ = typeof(Reads<Comp1>);
    }
}
