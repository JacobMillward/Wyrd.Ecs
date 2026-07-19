namespace Wyrd.Ecs.Tests;

public class WorldDirtyTrackingTests
{
    private struct Position : IComponent;
    private struct Marker : ITag;

    [Fact]
    public void GetComponent_MarksTheComponentDirtyAtTheCurrentTick()
    {
        var world = new World();
        using var tracking = world.TrackChanges<Position>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AdvanceTick();

        _ = world.GetComponent<Position>(entity);

        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[0].Should().Be(world.CurrentTick);
    }

    [Fact]
    public void AddComponent_MarksTheNewComponentDirtyAtTheCurrentTick()
    {
        var world = new World();
        using var tracking = world.TrackChanges<Position>();
        var entity = world.CreateEntity();

        world.AddComponent<Position>(entity);

        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[0].Should().Be(world.CurrentTick);
    }

    [Fact]
    public void StructuralMove_PreservesTheLastMarkedTick()
    {
        var world = new World();
        using var tracking = world.TrackChanges<Position>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity); // tick 1

        world.AddTag<Marker>(entity); // forces a structural move; Position's value must carry its tick across

        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        var (_, row) = TestReflection.GetLocation(world, entity);
        storage.RawLastMarkedTick[row].Should().Be(1);
    }

    [Fact]
    public void TryGetComponent_NeverMarksDirty()
    {
        var world = new World();
        using var tracking = world.TrackChanges<Position>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AdvanceTick();

        world.TryGetComponent<Position>(entity, out _);

        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[0].Should().NotBe(world.CurrentTick);
    }

    [Fact]
    public void GetComponent_WithNoRegisteredConsumer_NeverMarksDirty()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        var tickAfterAdd = storage.RawLastMarkedTick[0]; // AddComponent above also went unmarked
        world.AdvanceTick();

        _ = world.GetComponent<Position>(entity);

        storage.RawLastMarkedTick[0].Should().Be(tickAfterAdd);
        storage.RawLastMarkedTick[0].Should().NotBe(world.CurrentTick);
    }

    private static Wyrd.Ecs.Internal.Archetype GetArchetype(World world, Entity entity) =>
        TestReflection.GetLocation(world, entity).Archetype;
}
