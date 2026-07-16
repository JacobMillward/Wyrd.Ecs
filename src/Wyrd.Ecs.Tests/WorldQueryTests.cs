namespace Wyrd.Ecs.Tests;

public class WorldQueryTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private struct Velocity : IComponent
    {
        public float X;
    }

    private struct Marker : ITag;

    [Fact]
    public void OneComponent_VisitsEveryMatchingEntity()
    {
        var world = new World();
        for (var i = 0; i < 5; i++)
            world.AddComponent<Position>(world.CreateEntity()).X = i;

        var seen = new List<float>();
        foreach (var row in world.Query<Position>())
            seen.Add(row.Get<Position>().X);

        seen.Should().BeEquivalentTo(new[] { 0f, 1f, 2f, 3f, 4f });
    }

    [Fact]
    public void OneComponent_SkipsEntitiesWithoutTheComponent()
    {
        var world = new World();
        world.AddComponent<Position>(world.CreateEntity());
        world.CreateEntity(); // no Position

        var count = 0;
        foreach (var _ in world.Query<Position>())
            count++;

        count.Should().Be(1);
    }

    [Fact]
    public void OneComponent_WritesThroughToRealStorage()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;

        foreach (var row in world.Query<Position>())
            row.Get<Position>().X += 10f;

        world.GetComponent<Position>(entity).X.Should().Be(11f);
    }

    [Fact]
    public void OneComponent_GetMarksThatEntityDirty()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AdvanceTick();

        foreach (var row in world.Query<Position>())
            _ = row.Get<Position>();

        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[0].Should().Be(world.CurrentTick);
    }

    [Fact]
    public void OneComponent_TouchingAnEntityTwiceInOneTick_LogsExactlyOneEntry()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        var cursorAfterAdd = world.CurrentTick;
        world.AdvanceTick();

        foreach (var row in world.Query<Position>())
        {
            _ = row.Get<Position>();
            _ = row.Get<Position>();
        }

        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.ReadDirtyLogSince(cursorAfterAdd).ToArray().Should()
            .Equal(new DirtyEntry(entity, world.CurrentTick));
    }

    [Fact]
    public void OneComponent_RowExposesTheOwningEntity()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);

        var seen = new List<Entity>();
        foreach (var row in world.Query<Position>())
            seen.Add(row.Entity);

        seen.Should().Equal(entity);
    }

    [Fact]
    public void Get_WithATypeNotInTheQuery_Throws()
    {
        var world = new World();
        world.AddComponent<Position>(world.CreateEntity());

        var threw = false;
        foreach (var row in world.Query<Position>())
        {
            try
            {
                row.Get<Velocity>();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }
        }

        threw.Should().BeTrue();
    }

    [Fact]
    public void OneComponent_SpansMultipleArchetypes()
    {
        var world = new World();
        var onlyPosition = world.CreateEntity();
        world.AddComponent<Position>(onlyPosition).X = 1f;

        var withTag = world.CreateEntity();
        world.AddComponent<Position>(withTag).X = 2f;
        world.AddTag<Marker>(withTag);

        var seen = new List<float>();
        foreach (var row in world.Query<Position>())
            seen.Add(row.Get<Position>().X);

        seen.Should().BeEquivalentTo(new[] { 1f, 2f });
    }

    [Fact]
    public void ChunkTierAndUnifiedQuery_VisitTheSameEntities()
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

        var viaUnifiedQuery = new List<float>();
        foreach (var row in world.Query<Position>())
            viaUnifiedQuery.Add(row.Get<Position>().X);

        viaUnifiedQuery.Should().BeEquivalentTo(viaChunk);
    }

    [Fact]
    public void Query_EmptyArchetype_NeverYieldsARow()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.DestroyEntity(entity);

        var count = 0;
        foreach (var _ in world.Query<Position>())
            count++;

        count.Should().Be(0);
    }

    private static Wyrd.Ecs.Internal.Archetype GetArchetype(World world, Entity entity)
    {
        var field = typeof(World).GetField("_locations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var locations = (System.ValueTuple<Wyrd.Ecs.Internal.Archetype, int>[])field.GetValue(world)!;
        return locations[entity.Id].Item1;
    }
}
