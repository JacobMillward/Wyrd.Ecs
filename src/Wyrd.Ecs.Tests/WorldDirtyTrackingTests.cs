namespace Wyrd.Ecs.Tests;

public class WorldDirtyTrackingTests
{
    private struct Position : IComponent;

    [Fact]
    public void GetComponent_MarksTheComponentDirtyAtTheCurrentTick()
    {
        var world = new World();
        using var consumer = world.RegisterChangeConsumer<Position>();
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
        using var consumer = world.RegisterChangeConsumer<Position>();
        var entity = world.CreateEntity();

        world.AddComponent<Position>(entity);

        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.RawLastMarkedTick[0].Should().Be(world.CurrentTick);
    }

    [Fact]
    public void GetComponent_TouchedTwiceSameTick_LogsOnlyOneEntry()
    {
        var world = new World();
        using var consumer = world.RegisterChangeConsumer<Position>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        var cursorAfterAdd = world.CurrentTick;
        world.AdvanceTick();

        _ = world.GetComponent<Position>(entity);
        _ = world.GetComponent<Position>(entity);

        var archetype = GetArchetype(world, entity);
        var storage = archetype.Storages[Wyrd.Ecs.Internal.TypeIndex<Position>.Value];
        storage.ReadDirtyLogSince(cursorAfterAdd).ToArray().Should()
            .Equal(new DirtyEntry(entity, world.CurrentTick));
    }

    [Fact]
    public void TryGetComponent_NeverMarksDirty()
    {
        var world = new World();
        using var consumer = world.RegisterChangeConsumer<Position>();
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

    private static Wyrd.Ecs.Internal.Archetype GetArchetype(World world, Entity entity)
    {
        var field = typeof(World).GetField("_locations", global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.Instance)!;
        var locations = ((Wyrd.Ecs.Internal.Archetype Archetype, int Row)[])field.GetValue(world)!;
        return locations[entity.Id].Archetype;
    }
}
