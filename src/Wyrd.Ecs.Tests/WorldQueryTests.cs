namespace Wyrd.Ecs.Tests;

public class WorldQueryTests
{
    internal struct Position : IComponent
    {
        public float X;
    }

    internal struct Velocity : IComponent
    {
        public float X;
    }

    internal struct Acceleration : IComponent
    {
        public float X;
    }

    internal struct C4 : IComponent { public float X; }
    internal struct C5 : IComponent { public float X; }
    internal struct C6 : IComponent { public float X; }
    internal struct C7 : IComponent { public float X; }

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
        using var tracking = world.TrackChanges<Position>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AdvanceTick();

        foreach (var row in world.Query<Position>())
            row.Get<Position>().X += 1f;

        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[0].Should().Be(world.CurrentTick);
    }

    [Fact]
    public void OneComponent_WithNoRegisteredConsumer_NeverMarksDirty()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AdvanceTick();

        foreach (var row in world.Query<Position>())
            row.Get<Position>().X += 1f;

        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[0].Should().NotBe(world.CurrentTick);
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

    [Fact]
    public void TwoComponent_RequiresBothComponents()
    {
        var world = new World();
        var both = world.CreateEntity();
        world.AddComponent<Position>(both).X = 1f;
        world.AddComponent<Velocity>(both).X = 2f;
        var positionOnly = world.CreateEntity();
        world.AddComponent<Position>(positionOnly);

        var visited = new List<Entity>();
        foreach (var row in world.Query<Position, Velocity>())
            visited.Add(row.Entity);

        visited.Should().Equal(both);
    }

    [Fact]
    public void TwoComponent_GetReadsBothComponentsCorrectly()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 3f;
        world.AddComponent<Velocity>(entity).X = 4f;

        var sums = new List<float>();
        foreach (var row in world.Query<Position, Velocity>())
            sums.Add(row.Get<Position>().X + row.Get<Velocity>().X);

        sums.Should().Equal(7f);
    }

    [Fact]
    public void TwoComponent_GettingOneComponentDoesNotMarkTheOtherDirty()
    {
        var world = new World();
        using var positionConsumer = world.TrackChanges<Position>();
        using var velocityConsumer = world.TrackChanges<Velocity>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AddComponent<Velocity>(entity);
        world.AdvanceTick();

        foreach (var row in world.Query<Position, Velocity>())
            _ = row.Get<Position>();

        var archetype = GetArchetype(world, entity);
        var velocityStorage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Velocity>.Value];
        velocityStorage.RawLastMarkedTick[0].Should().NotBe(world.CurrentTick);
    }

    [Fact]
    public void TwoComponent_Deconstructs()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 5f;
        world.AddComponent<Velocity>(entity).X = 6f;

        var sums = new List<float>();
        foreach (var (position, velocity) in world.Query<Position, Velocity>())
            sums.Add(position.X + velocity.X);

        sums.Should().Equal(11f);
    }

    [Fact]
    public void TwoComponent_DeconstructNeverMarksDirty()
    {
        var world = new World();
        using var positionConsumer = world.TrackChanges<Position>();
        using var velocityConsumer = world.TrackChanges<Velocity>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AddComponent<Velocity>(entity);
        world.AdvanceTick();

        foreach (var (position, velocity) in world.Query<Position, Velocity>())
            _ = (position, velocity);

        var archetype = GetArchetype(world, entity);
        var positionStorage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        var velocityStorage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Velocity>.Value];
        positionStorage.RawLastMarkedTick[0].Should().NotBe(world.CurrentTick);
        velocityStorage.RawLastMarkedTick[0].Should().NotBe(world.CurrentTick);
    }

    [Fact]
    public void WorldIndexer_GetsTheCurrentComponentByEntity()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 9f;

        world[entity].GetComponent<Position>().X.Should().Be(9f);
    }

    [Fact]
    public void WorldIndexer_WritesThroughToRealStorage()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;

        world[entity].GetComponent<Position>().X += 10f;

        world.GetComponent<Position>(entity).X.Should().Be(11f);
    }

    [Fact]
    public void WorldIndexer_ResolvesCorrectlyAfterAStructuralMove()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;
        world.AddTag<Marker>(entity); // forces a structural move to a different archetype

        world[entity].GetComponent<Position>().X.Should().Be(1f);
    }

    [Fact]
    public void ThreeComponent_RequiresAllThreeComponents()
    {
        var world = new World();
        var all = world.CreateEntity();
        world.AddComponent<Position>(all);
        world.AddComponent<Velocity>(all);
        world.AddComponent<Acceleration>(all);
        var missingOne = world.CreateEntity();
        world.AddComponent<Position>(missingOne);
        world.AddComponent<Velocity>(missingOne);

        var visited = new List<Entity>();
        foreach (var row in world.Query<Position, Velocity, Acceleration>())
            visited.Add(row.Entity);

        visited.Should().Equal(all);
    }

    [Fact]
    public void ThreeComponent_GetReadsAllThreeCorrectly()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;
        world.AddComponent<Velocity>(entity).X = 2f;
        world.AddComponent<Acceleration>(entity).X = 3f;

        var sums = new List<float>();
        foreach (var row in world.Query<Position, Velocity, Acceleration>())
            sums.Add(row.Get<Position>().X + row.Get<Velocity>().X + row.Get<Acceleration>().X);

        sums.Should().Equal(6f);
    }

    [Fact]
    public void ThreeComponent_MarksOnlyTheComponentTouched()
    {
        var world = new World();
        using var positionConsumer = world.TrackChanges<Position>();
        using var velocityConsumer = world.TrackChanges<Velocity>();
        using var accelerationConsumer = world.TrackChanges<Acceleration>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AddComponent<Velocity>(entity);
        world.AddComponent<Acceleration>(entity);
        world.AdvanceTick();

        foreach (var row in world.Query<Position, Velocity, Acceleration>())
            row.Get<Velocity>().X += 1f;

        var archetype = GetArchetype(world, entity);
        archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value].RawLastMarkedTick[0].Should().NotBe(world.CurrentTick);
        archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Velocity>.Value].RawLastMarkedTick[0].Should().Be(world.CurrentTick);
        archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Acceleration>.Value].RawLastMarkedTick[0].Should().NotBe(world.CurrentTick);
    }

    [Fact]
    public void ThreeComponent_Deconstructs()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;
        world.AddComponent<Velocity>(entity).X = 2f;
        world.AddComponent<Acceleration>(entity).X = 3f;

        var sums = new List<float>();
        foreach (var (position, velocity, acceleration) in world.Query<Position, Velocity, Acceleration>())
            sums.Add(position.X + velocity.X + acceleration.X);

        sums.Should().Equal(6f);
    }

    [Fact]
    public void EightComponent_RequiresAllEightComponents()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;
        world.AddComponent<Velocity>(entity).X = 1f;
        world.AddComponent<Acceleration>(entity).X = 1f;
        world.AddComponent<C4>(entity).X = 1f;
        world.AddComponent<C5>(entity).X = 1f;
        world.AddComponent<C6>(entity).X = 1f;
        world.AddComponent<C7>(entity).X = 1f;
        world.AddTag<Marker>(entity);

        var missingOne = world.CreateEntity();
        world.AddComponent<Position>(missingOne).X = 1f;
        world.AddComponent<Velocity>(missingOne).X = 1f;
        world.AddComponent<Acceleration>(missingOne).X = 1f;
        world.AddComponent<C4>(missingOne).X = 1f;
        world.AddComponent<C5>(missingOne).X = 1f;
        world.AddComponent<C6>(missingOne).X = 1f;
        // no C7

        var sums = new List<float>();
        foreach (var row in world.Query<Position, Velocity, Acceleration, C4, C5, C6, C7>())
            sums.Add(row.Get<Position>().X + row.Get<Velocity>().X + row.Get<Acceleration>().X
                + row.Get<C4>().X + row.Get<C5>().X + row.Get<C6>().X + row.Get<C7>().X);

        sums.Should().Equal(7f);
    }

    [Fact]
    public void GetUnmarked_ReadsTheCurrentValueWithoutMarkingDirty()
    {
        var world = new World();
        using var consumer = world.TrackChanges<Position>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 7f;
        world.AdvanceTick();

        var seen = 0f;
        foreach (var row in world.Query<Position>())
            seen = row.GetUnmarked<Position>().X;

        seen.Should().Be(7f);
        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[0].Should().NotBe(world.CurrentTick);
    }

    [Fact]
    public void GetUnmarked_WritesThroughToRealStorageAnyway()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;

        foreach (var row in world.Query<Position>())
            row.GetUnmarked<Position>().X += 10f;

        world.GetComponent<Position>(entity).X.Should().Be(11f);
    }

    private static Wyrd.Ecs.Internal.Archetype GetArchetype(World world, Entity entity) =>
        TestReflection.GetLocation(world, entity).Archetype;
}
