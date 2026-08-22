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

        Entity entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();

        world.HasComponent<Position>(entity).Should().BeTrue();
        world.GetComponent<Position>(entity).X.Should().Be(5f);
    }

    [Fact]
    public void CreateEntity_WithTwoComponents_SetsBothDirectly()
    {
        var world = new World();

        Entity entity = world.Commands.CreateEntity(new Position { X = 1f }, new Velocity { X = 2f });
        world.ApplyCommands();

        world.GetComponent<Position>(entity).X.Should().Be(1f);
        world.GetComponent<Velocity>(entity).X.Should().Be(2f);
    }

    [Fact]
    public void CreateEntity_WithComponents_IsAlive()
    {
        var world = new World();

        Entity entity = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();

        world.IsAlive(entity).Should().BeTrue();
    }

    [Fact]
    public void CreateEntity_WithComponents_TwoEntitiesOfTheSameShape_ShareOneArchetype()
    {
        var world = new World();

        Entity a = world.Commands.CreateEntity(new Position { X = 1f }, new Velocity { X = 2f });
        Entity b = world.Commands.CreateEntity(new Position { X = 3f }, new Velocity { X = 4f });
        world.ApplyCommands();

        var visited = new List<Entity>();
        foreach (var chunk in ArchetypeQuery.Empty.Access<Ref<Position>>().Access<Ref<Velocity>>().Resolve(world))
            visited.AddRange(chunk.Entities.ToArray());

        visited.Should().BeEquivalentTo(new[] { a, b });
    }

    [Fact]
    public void CreateEntity_WithComponents_WithTrackingOff_NeverMarksDirty()
    {
        var world = new World();

        Entity entity = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();

        var (archetype, row) = TestReflection.GetLocation(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[row].Should().Be(0);
    }

    [Fact]
    public void CreateEntity_WithComponents_WithTrackingOn_MarksDirtyAtTheCurrentTick()
    {
        var world = new World();
        using var consumer = world.TrackChanges<Position>();

        Entity entity = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();

        var (archetype, row) = TestReflection.GetLocation(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[row].Should().Be(world.CurrentTick);
    }

    [Fact]
    public void CreateEntity_ReturnsANonNullEntity()
    {
        var world = new World();

        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        entity.IsNull.Should().BeFalse();
    }

    [Fact]
    public void CreateEntity_IsAlive()
    {
        var world = new World();

        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.IsAlive(entity).Should().BeTrue();
    }

    [Fact]
    public void CreateEntity_TwiceInARow_ReturnsDistinctEntities()
    {
        var world = new World();

        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        a.Should().NotBe(b);
    }

    [Fact]
    public void CreateEntity_AssignsAUniquePermanentId()
    {
        var world = new World();

        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.GetPermanentId(a).Should().NotBe(world.GetPermanentId(b));
    }

    [Fact]
    public void DestroyEntity_IsNoLongerAlive()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.DestroyEntity(entity);
        world.ApplyCommands();

        world.IsAlive(entity).Should().BeFalse();
    }

    [Fact]
    public void DestroyEntity_ReusedIdGetsANewGeneration_OldHandleStaysDead()
    {
        var world = new World();
        Entity first = world.Commands.CreateEntity();
        world.ApplyCommands();
        world.Commands.DestroyEntity(first);
        world.ApplyCommands();

        Entity second = world.Commands.CreateEntity();
        world.ApplyCommands();

        second.Id.Should().Be(first.Id);
        second.Generation.Should().NotBe(first.Generation);
        world.IsAlive(first).Should().BeFalse();
        world.IsAlive(second).Should().BeTrue();
    }

    [Fact]
    public void DestroyEntity_MiddleOfMany_KeepsOthersAlive()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        Entity b = world.Commands.CreateEntity();
        Entity c = world.Commands.CreateEntity();
        world.ApplyCommands();

        world.Commands.DestroyEntity(b);
        world.ApplyCommands();

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
    public void DestroyEntity_NotAlive_IsSilentlyANoOp()
    {
        var world = new World();

        world.Commands.DestroyEntity(new Entity(1, 0));
        var act = () => world.ApplyCommands();

        act.Should().NotThrow();
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
                world.Commands.DestroyEntity(victim);
                world.ApplyCommands();
                live.Remove(victim);
            }
            else
            {
                Entity created = world.Commands.CreateEntity();
                world.ApplyCommands();
                live.Add(created).Should().BeTrue();
            }
        }

        foreach (var entity in live)
            world.IsAlive(entity).Should().BeTrue();
    }

    [Fact]
    public void TotalEntityCount_SumsAcrossArchetypes()
    {
        var world = new World();
        Entity a = world.Commands.CreateEntity();
        world.Commands.AddComponent(a, new Position { X = 1f });
        world.Commands.CreateEntity(); // stays in the empty archetype
        world.ApplyCommands();

        TestReflection.GetTotalEntityCount(world).Should().Be(2);
    }

    [Fact]
    public void RecycledEntity_GetsFreshPermanentIdentity()
    {
        var world = new World();
        Entity first = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();
        var firstId = world.GetPermanentId(first);

        world.Commands.DestroyEntity(first);
        world.ApplyCommands();

        Entity second = world.Commands.CreateEntity(new Position());
        world.ApplyCommands();

        // Same slot, new incarnation: the permanent identity must never be reused across
        // incarnations - persistence and WAL replay key entities by it.
        second.Id.Should().Be(first.Id);
        world.GetPermanentId(second).Should().NotBe(firstId);
    }
}
