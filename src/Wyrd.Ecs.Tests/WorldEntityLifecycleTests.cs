namespace Wyrd.Ecs.Tests;

public class WorldEntityLifecycleTests
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
    public void CreateEntity_WithOneComponent_SetsItDirectly()
    {
        var world = new World();

        var entity = world.CreateEntity(new Position { X = 5f });

        world.HasComponent<Position>(entity).Should().BeTrue();
        world.GetComponent<Position>(entity).X.Should().Be(5f);
    }

    [Fact]
    public void CreateEntity_WithTwoComponents_SetsBothDirectly()
    {
        var world = new World();

        var entity = world.CreateEntity(new Position { X = 1f }, new Velocity { X = 2f });

        world.GetComponent<Position>(entity).X.Should().Be(1f);
        world.GetComponent<Velocity>(entity).X.Should().Be(2f);
    }

    [Fact]
    public void CreateEntity_WithComponents_IsAlive()
    {
        var world = new World();

        var entity = world.CreateEntity(new Position());

        world.IsAlive(entity).Should().BeTrue();
    }

    [Fact]
    public void CreateEntity_WithComponents_TwoEntitiesOfTheSameShape_ShareOneArchetype()
    {
        var world = new World();

        var a = world.CreateEntity(new Position { X = 1f }, new Velocity { X = 2f });
        var b = world.CreateEntity(new Position { X = 3f }, new Velocity { X = 4f });

        var visited = new List<Entity>();
        foreach (var row in world.Query<Position, Velocity>())
            visited.Add(row.Entity);

        visited.Should().BeEquivalentTo(new[] { a, b });
    }

    [Fact]
    public void CreateEntity_WithComponents_WithTrackingOff_NeverMarksDirty()
    {
        var world = new World();

        var entity = world.CreateEntity(new Position());

        var (archetype, row) = TestReflection.GetLocation(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[row].Should().Be(0);
    }

    [Fact]
    public void CreateEntity_WithComponents_WithTrackingOn_MarksDirtyAtTheCurrentTick()
    {
        var world = new World();
        using var consumer = world.TrackChanges<Position>();

        var entity = world.CreateEntity(new Position());

        var (archetype, row) = TestReflection.GetLocation(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[row].Should().Be(world.CurrentTick);
    }

    [Fact]
    public void CreateEntity_ReturnsANonNullEntity()
    {
        var world = new World();

        var entity = world.CreateEntity();

        entity.IsNull.Should().BeFalse();
    }

    [Fact]
    public void CreateEntity_IsAlive()
    {
        var world = new World();

        var entity = world.CreateEntity();

        world.IsAlive(entity).Should().BeTrue();
    }

    [Fact]
    public void CreateEntity_TwiceInARow_ReturnsDistinctEntities()
    {
        var world = new World();

        var a = world.CreateEntity();
        var b = world.CreateEntity();

        a.Should().NotBe(b);
    }

    [Fact]
    public void CreateEntity_AssignsAUniquePermanentId()
    {
        var world = new World();

        var a = world.CreateEntity();
        var b = world.CreateEntity();

        world.GetPermanentId(a).Should().NotBe(world.GetPermanentId(b));
    }

    [Fact]
    public void DestroyEntity_IsNoLongerAlive()
    {
        var world = new World();
        var entity = world.CreateEntity();

        world.DestroyEntity(entity);

        world.IsAlive(entity).Should().BeFalse();
    }

    [Fact]
    public void DestroyEntity_ReusedIdGetsANewGeneration_OldHandleStaysDead()
    {
        var world = new World();
        var first = world.CreateEntity();
        world.DestroyEntity(first);

        var second = world.CreateEntity();

        second.Id.Should().Be(first.Id);
        second.Generation.Should().NotBe(first.Generation);
        world.IsAlive(first).Should().BeFalse();
        world.IsAlive(second).Should().BeTrue();
    }

    [Fact]
    public void DestroyEntity_MiddleOfMany_KeepsOthersAlive()
    {
        var world = new World();
        var a = world.CreateEntity();
        var b = world.CreateEntity();
        var c = world.CreateEntity();

        world.DestroyEntity(b);

        world.IsAlive(a).Should().BeTrue();
        world.IsAlive(c).Should().BeTrue();
        world.IsAlive(b).Should().BeFalse();
    }

    [Fact]
    public void IsAlive_NullEntity_IsFalse()
    {
        var world = new World();

        world.IsAlive(Entity.Null).Should().BeFalse();
    }

    [Fact]
    public void IsAlive_NeverCreatedEntity_IsFalse()
    {
        var world = new World();

        world.IsAlive(new Entity(9999, 0)).Should().BeFalse();
    }

    [Fact]
    public void DestroyEntity_NotAlive_Throws()
    {
        var world = new World();

        var act = () => world.DestroyEntity(new Entity(1, 0));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ManyCreatesAndDestroys_NeverProducesADuplicateLiveEntity()
    {
        var world = new World();
        var live = new HashSet<Entity>();

        var random = new Random(1234);
        for (var i = 0; i < 5_000; i++)
        {
            if (live.Count > 0 && random.Next(2) == 0)
            {
                var victim = live.First();
                world.DestroyEntity(victim);
                live.Remove(victim);
            }
            else
            {
                var created = world.CreateEntity();
                live.Add(created).Should().BeTrue();
            }
        }

        foreach (var entity in live)
            world.IsAlive(entity).Should().BeTrue();
    }
}
