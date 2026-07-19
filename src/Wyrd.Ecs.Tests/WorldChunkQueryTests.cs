namespace Wyrd.Ecs.Tests;

public class WorldChunkQueryTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private struct Velocity : IComponent
    {
        public float X;
    }

    [Fact]
    public void OneComponentQuery_VisitsEveryMatchingEntity()
    {
        var world = new World();
        for (var i = 0; i < 5; i++)
            world.AddComponent<Position>(world.CreateEntity()).X = i;

        var seen = new List<float>();
        world.Query<Mut<Position>>(chunk =>
        {
            for (var i = 0; i < chunk.Length; i++)
                seen.Add(chunk[i].X);
        });

        seen.Should().BeEquivalentTo(new[] { 0f, 1f, 2f, 3f, 4f });
    }

    [Fact]
    public void OneComponentQuery_SkipsEntitiesWithoutTheComponent()
    {
        var world = new World();
        world.AddComponent<Position>(world.CreateEntity());
        world.CreateEntity(); // no Position

        var visitCount = 0;
        world.Query<Ref<Position>>(_ => visitCount++);

        visitCount.Should().Be(1);
    }

    [Fact]
    public void OneComponentQuery_MutatesTheRealStorage()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;

        world.Query<Mut<Position>>(chunk =>
        {
            for (var i = 0; i < chunk.Length; i++)
                chunk[i].X += 10f;
        });

        world.GetComponent<Position>(entity).X.Should().Be(11f);
    }

    [Fact]
    public void RefQuery_NeverMarksAnythingDirty()
    {
        var world = new World();
        using var consumer = world.TrackChanges<Position>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;
        world.AdvanceTick();

        world.Query<Ref<Position>>(chunk =>
        {
            for (var i = 0; i < chunk.Length; i++)
                _ = chunk[i].X;
        });

        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[0].Should().NotBe(world.CurrentTick);
    }

    [Fact]
    public void MutQuery_TouchingOnlySomeEntities_MarksOnlyThoseDirty()
    {
        var world = new World();
        using var consumer = world.TrackChanges<Position>();
        var entities = new Entity[3];
        for (var i = 0; i < 3; i++)
        {
            entities[i] = world.CreateEntity();
            world.AddComponent<Position>(entities[i]);
        }
        world.AdvanceTick();

        world.Query<Mut<Position>>(chunk => { chunk[0].X += 0f; });

        var archetype = GetArchetype(world, entities[0]);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[0].Should().Be(world.CurrentTick);
        storage.RawLastMarkedTick[1].Should().NotBe(world.CurrentTick);
        storage.RawLastMarkedTick[2].Should().NotBe(world.CurrentTick);
    }

    [Fact]
    public void MutQuery_WithTrackingOff_NeverMarksAnythingDirty()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 1f;
        world.AdvanceTick();

        world.Query<Mut<Position>>(chunk => { chunk[0].X += 1f; });

        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[0].Should().NotBe(world.CurrentTick);
        world.GetComponent<Position>(entity).X.Should().Be(2f); // the write itself still went through
    }

    private static Wyrd.Ecs.Internal.Archetype GetArchetype(World world, Entity entity) =>
        TestReflection.GetLocation(world, entity).Archetype;

    [Fact]
    public void TwoComponentQuery_RequiresBothComponents()
    {
        var world = new World();
        var both = world.CreateEntity();
        world.AddComponent<Position>(both).X = 1f;
        world.AddComponent<Velocity>(both).X = 2f;
        var positionOnly = world.CreateEntity();
        world.AddComponent<Position>(positionOnly);

        var visited = new List<Entity>();
        world.Query<Ref<Position>, Ref<Velocity>>((position, velocity) =>
        {
            for (var i = 0; i < position.Length; i++)
                visited.Add(both);
        });

        visited.Should().ContainSingle();
    }

    [Fact]
    public void TwoComponentQuery_ReadsBothComponentsCorrectly()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity).X = 3f;
        world.AddComponent<Velocity>(entity).X = 4f;

        var sums = new List<float>();
        world.Query<Ref<Position>, Ref<Velocity>>((position, velocity) =>
        {
            for (var i = 0; i < position.Length; i++)
                sums.Add(position[i].X + velocity[i].X);
        });

        sums.Should().Equal(7f);
    }

    [Fact]
    public void Query_EmptyArchetype_NeverInvokesTheCallback()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.DestroyEntity(entity);

        var invoked = false;
        world.Query<Mut<Position>>(_ => invoked = true);

        invoked.Should().BeFalse();
    }
}
