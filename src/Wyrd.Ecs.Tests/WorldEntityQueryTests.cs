namespace Wyrd.Ecs.Tests;

public class WorldEntityQueryTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private struct Marker : ITag;

    [Fact]
    public void QueryRef_VisitsEveryMatchingEntity()
    {
        var world = new World();
        for (var i = 0; i < 5; i++)
            world.AddComponent<Position>(world.CreateEntity()).X = i;

        var seen = new List<float>();
        foreach (var position in world.QueryRef<Position>())
            seen.Add(position.X);

        seen.Should().BeEquivalentTo(new[] { 0f, 1f, 2f, 3f, 4f });
    }

    [Fact]
    public void QueryRef_SkipsEntitiesWithoutTheComponent()
    {
        var world = new World();
        world.AddComponent<Position>(world.CreateEntity());
        world.CreateEntity(); // no Position

        var count = 0;
        foreach (var _ in world.QueryRef<Position>())
            count++;

        count.Should().Be(1);
    }

    [Fact]
    public void QueryMut_WritesThroughToRealStorage()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;

        foreach (ref var position in world.QueryMut<Position>())
            position.X += 10f;

        world.GetComponent<Position>(entity).X.Should().Be(11f);
    }

    [Fact]
    public void QueryMut_AccessingCurrent_MarksThatEntityDirty()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);

        foreach (ref var position in world.QueryMut<Position>())
            _ = position;

        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawDirty[0].Should().BeTrue();
    }

    [Fact]
    public void QueryRef_NeverMarksDirty()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;

        foreach (var position in world.QueryRef<Position>())
            _ = position.X;

        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawDirty[0].Should().BeFalse();
    }

    [Fact]
    public void QueryRef_SpansMultipleArchetypes()
    {
        var world = new World();
        var onlyPosition = world.CreateEntity();
        world.AddComponent<Position>(onlyPosition).X = 1f;

        var withTag = world.CreateEntity();
        world.AddComponent<Position>(withTag).X = 2f;
        world.AddTag<Marker>(withTag);

        var seen = new List<float>();
        foreach (var position in world.QueryRef<Position>())
            seen.Add(position.X);

        seen.Should().BeEquivalentTo(new[] { 1f, 2f });
    }

    [Fact]
    public void ChunkTierAndEntityTier_VisitTheSameEntities()
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

        var viaEntityTier = new List<float>();
        foreach (var position in world.QueryRef<Position>())
            viaEntityTier.Add(position.X);

        viaEntityTier.Should().BeEquivalentTo(viaChunk);
    }

    private static Wyrd.Ecs.Internal.Archetype GetArchetype(World world, Entity entity)
    {
        var field = typeof(World).GetField("_locations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var locations = (System.ValueTuple<Wyrd.Ecs.Internal.Archetype, int>[])field.GetValue(world)!;
        return locations[entity.Id].Item1;
    }
}
