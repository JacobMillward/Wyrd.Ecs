namespace Wyrd.Ecs.Tests;

public class WorldEntityQueryTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    [Fact]
    public void HiddenChunkQuery_VisitsEveryMatchingEntity()
    {
        var world = new World();
        for (var i = 0; i < 5; i++)
            world.AddComponent<Position>(world.CreateEntity()).X = i;

        var seen = new List<float>();
        foreach (var position in world.Query<Ref<Position>>())
            seen.Add(position[0].X);

        seen.Should().BeEquivalentTo(new[] { 0f, 1f, 2f, 3f, 4f });
    }

    [Fact]
    public void HiddenChunkQuery_SkipsEntitiesWithoutTheComponent()
    {
        var world = new World();
        world.AddComponent<Position>(world.CreateEntity());
        world.CreateEntity(); // no Position

        var count = 0;
        foreach (var _ in world.Query<Ref<Position>>())
            count++;

        count.Should().Be(1);
    }

    [Fact]
    public void HiddenChunkQuery_YieldsLengthOneAccessorsPerEntity()
    {
        var world = new World();
        world.AddComponent<Position>(world.CreateEntity());

        foreach (var position in world.Query<Ref<Position>>())
            position.Length.Should().Be(1);
    }

    [Fact]
    public void HiddenChunkQuery_MutVariant_WritesThroughToRealStorage()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;

        foreach (var position in world.Query<Mut<Position>>())
            position[0].X += 10f;

        world.GetComponent<Position>(entity).X.Should().Be(11f);
    }

    [Fact]
    public void HiddenChunkQuery_SpansMultipleArchetypes()
    {
        var world = new World();
        var onlyPosition = world.CreateEntity();
        world.AddComponent<Position>(onlyPosition).X = 1f;

        var withTag = world.CreateEntity();
        world.AddComponent<Position>(withTag).X = 2f;
        world.AddTag<Marker>(withTag);

        var seen = new List<float>();
        foreach (var position in world.Query<Ref<Position>>())
            seen.Add(position[0].X);

        seen.Should().BeEquivalentTo(new[] { 1f, 2f });
    }

    [Fact]
    public void ChunkTierAndHiddenChunkTier_VisitTheSameEntities()
    {
        var world = new World();
        for (var i = 0; i < 20; i++)
        {
            var entity = world.CreateEntity();
            world.AddComponent<Position>(entity).X = i;
            if (i % 3 == 0) world.AddTag<Marker>(entity);
        }

        var viaChunk = new List<float>();
        world.Query<Ref<Position>>(chunk =>
        {
            for (var i = 0; i < chunk.Length; i++)
                viaChunk.Add(chunk[i].X);
        });

        var viaHiddenChunk = new List<float>();
        foreach (var position in world.Query<Ref<Position>>())
            viaHiddenChunk.Add(position[0].X);

        viaHiddenChunk.Should().BeEquivalentTo(viaChunk);
    }

    private struct Marker : ITag;
}
