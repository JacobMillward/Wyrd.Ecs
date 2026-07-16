namespace Wyrd.Ecs.Tests;

public class WorldDirtyReadTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    private struct Velocity : IComponent;

    [Fact]
    public void ReadDirty_SinceZero_ReturnsEveryDirtyEntity()
    {
        var world = new World();
        var a = world.CreateEntity();
        var b = world.CreateEntity();
        world.AddComponent<Position>(a);
        world.AddComponent<Position>(b);

        var seen = new List<Entity>();
        foreach (var entry in world.ReadDirty<Position>(sinceTick: 0))
            seen.Add(entry.Entity);

        seen.Should().BeEquivalentTo(new[] { a, b });
    }

    [Fact]
    public void ReadDirty_SinceCurrentTick_ReturnsNothingUntilTheNextTick()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);

        var seen = new List<Entity>();
        foreach (var entry in world.ReadDirty<Position>(sinceTick: world.CurrentTick))
            seen.Add(entry.Entity);

        seen.Should().BeEmpty();
    }

    [Fact]
    public void ReadDirty_SkipsEntriesFromBeforeTheCursor()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity); // recorded at tick 1
        var cursorAfterFirstWrite = world.CurrentTick;

        world.AdvanceTick();
        world.GetComponent<Position>(entity).X = 2f; // recorded at tick 2

        var seenTicks = new List<int>();
        foreach (var entry in world.ReadDirty<Position>(cursorAfterFirstWrite))
            seenTicks.Add(entry.Tick);

        seenTicks.Should().Equal(2);
    }

    [Fact]
    public void ReadDirty_SpansMultipleArchetypes()
    {
        var world = new World();
        var onlyPosition = world.CreateEntity();
        world.AddComponent<Position>(onlyPosition);

        var withVelocity = world.CreateEntity();
        world.AddComponent<Position>(withVelocity);
        world.AddComponent<Velocity>(withVelocity);

        var seen = new List<Entity>();
        foreach (var entry in world.ReadDirty<Position>(sinceTick: 0))
            seen.Add(entry.Entity);

        seen.Should().BeEquivalentTo(new[] { onlyPosition, withVelocity });
    }

    [Fact]
    public void ReadDirty_ReflectsChunkQueryMutations()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AdvanceTick();

        world.Query<Mut<Position>>(chunk => { chunk[0].X += 0f; });

        var seen = new List<Entity>();
        foreach (var entry in world.ReadDirty<Position>(sinceTick: 0))
            seen.Add(entry.Entity);

        seen.Should().Contain(entity);
    }

    [Fact]
    public void ReadDirty_ReflectsHiddenChunkQueryMutations()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AdvanceTick();

        foreach (ref var position in world.QueryMut<Position>())
            _ = position;

        var seen = new List<Entity>();
        foreach (var entry in world.ReadDirty<Position>(sinceTick: 0))
            seen.Add(entry.Entity);

        seen.Should().Contain(entity);
    }
}
