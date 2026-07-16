namespace Wyrd.Ecs.Tests;

public class WorldDirtyReadTests
{
    internal struct Position : IComponent
    {
        public float X;
    }

    internal struct Velocity : IComponent;

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
        var cursorAfterAdd = world.CurrentTick;
        world.AdvanceTick();

        foreach (var row in world.Query<Position>())
            row.Get<Position>().X += 1f;

        var seen = new List<Entity>();
        foreach (var entry in world.ReadDirty<Position>(cursorAfterAdd))
            seen.Add(entry.Entity);

        seen.Should().Contain(entity);
    }

    [Fact]
    public void ReadDirty_TwoIndependentCursors_BothSeeTheSameEntryWithoutInterference()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);

        var firstReaderSeen = new List<Entity>();
        foreach (var entry in world.ReadDirty<Position>(sinceTick: 0))
            firstReaderSeen.Add(entry.Entity);

        // A second, independent reader starting fresh from 0 must see the same entry —
        // the first reader having already "read" it must not consume or hide it.
        var secondReaderSeen = new List<Entity>();
        foreach (var entry in world.ReadDirty<Position>(sinceTick: 0))
            secondReaderSeen.Add(entry.Entity);

        firstReaderSeen.Should().Equal(entity);
        secondReaderSeen.Should().Equal(entity);
    }

    [Fact]
    public void ReadDirty_TwoCursorsAtDifferentPositions_EachSeeOnlyTheirOwnNewEntries()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity); // tick 1
        var slowConsumerCursor = 0; // has never read anything

        world.AdvanceTick();
        world.GetComponent<Position>(entity).X = 2f; // tick 2
        var fastConsumerCursor = world.CurrentTick; // has already caught up to tick 2

        world.AdvanceTick();
        world.GetComponent<Position>(entity).X = 3f; // tick 3

        var slowConsumerSeenTicks = new List<int>();
        foreach (var entry in world.ReadDirty<Position>(slowConsumerCursor))
            slowConsumerSeenTicks.Add(entry.Tick);

        var fastConsumerSeenTicks = new List<int>();
        foreach (var entry in world.ReadDirty<Position>(fastConsumerCursor))
            fastConsumerSeenTicks.Add(entry.Tick);

        slowConsumerSeenTicks.Should().Equal(1, 2, 3);
        fastConsumerSeenTicks.Should().Equal(3);
    }
}
