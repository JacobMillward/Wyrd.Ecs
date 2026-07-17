namespace Wyrd.Ecs.Tests;

public class WorldDirtyReadTests
{
    internal struct Position : IComponent
    {
        public float X;
    }

    internal struct Velocity : IComponent;

    [Fact]
    public void ReadChanges_RegisteredAfterAWrite_DoesNotSeeThatWrite()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AdvanceTick();

        using var consumer = world.RegisterChangeConsumer<Position>();

        var seen = new List<Entity>();
        foreach (var entry in consumer.ReadChanges())
            seen.Add(entry.Entity);

        seen.Should().BeEmpty();
    }

    [Fact]
    public void Advance_SkipsEntriesFromBeforeTheNewPosition()
    {
        var world = new World();
        using var consumer = world.RegisterChangeConsumer<Position>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity); // recorded at tick 1
        var afterFirstWrite = world.CurrentTick;

        world.AdvanceTick();
        world.GetComponent<Position>(entity).X = 2f; // recorded at tick 2

        consumer.Advance(afterFirstWrite);

        var seenTicks = new List<int>();
        foreach (var entry in consumer.ReadChanges())
            seenTicks.Add(entry.Tick);

        seenTicks.Should().Equal(2);
    }

    [Fact]
    public void ReadChanges_SpansMultipleArchetypes()
    {
        var world = new World();
        using var consumer = world.RegisterChangeConsumer<Position>();
        world.AdvanceTick(); // entries recorded in the same tick as registration are never visible to it

        var onlyPosition = world.CreateEntity();
        world.AddComponent<Position>(onlyPosition);

        var withVelocity = world.CreateEntity();
        world.AddComponent<Position>(withVelocity);
        world.AddComponent<Velocity>(withVelocity);

        var seen = new List<Entity>();
        foreach (var entry in consumer.ReadChanges())
            seen.Add(entry.Entity);

        seen.Should().BeEquivalentTo(new[] { onlyPosition, withVelocity });
    }

    [Fact]
    public void ReadChanges_ReflectsChunkQueryMutations()
    {
        var world = new World();
        using var consumer = world.RegisterChangeConsumer<Position>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AdvanceTick();

        world.Query<Mut<Position>>(chunk => { chunk[0].X += 0f; });

        var seen = new List<Entity>();
        foreach (var entry in consumer.ReadChanges())
            seen.Add(entry.Entity);

        seen.Should().Contain(entity);
    }

    [Fact]
    public void ReadChanges_ReflectsHiddenChunkQueryMutations()
    {
        var world = new World();
        using var consumer = world.RegisterChangeConsumer<Position>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AdvanceTick();

        foreach (var row in world.Query<Position>())
            row.Get<Position>().X += 1f;

        var seen = new List<Entity>();
        foreach (var entry in consumer.ReadChanges())
            seen.Add(entry.Entity);

        seen.Should().Contain(entity);
    }

    [Fact]
    public void TwoIndependentConsumers_BothSeeTheSameEntryWithoutInterference()
    {
        var world = new World();
        var entity = world.CreateEntity();

        using var first = world.RegisterChangeConsumer<Position>();
        using var second = world.RegisterChangeConsumer<Position>();
        world.AdvanceTick(); // entries recorded in the same tick as registration are never visible to it
        world.AddComponent<Position>(entity);

        var firstSeen = new List<Entity>();
        foreach (var entry in first.ReadChanges())
            firstSeen.Add(entry.Entity);

        var secondSeen = new List<Entity>();
        foreach (var entry in second.ReadChanges())
            secondSeen.Add(entry.Entity);

        firstSeen.Should().Equal(entity);
        secondSeen.Should().Equal(entity);
    }

    [Fact]
    public void TwoConsumersAtDifferentPositions_EachSeeOnlyTheirOwnNewEntries()
    {
        var world = new World();
        var entity = world.CreateEntity();

        using var slowConsumer = world.RegisterChangeConsumer<Position>();
        world.AdvanceTick(); // entries recorded in the same tick as registration are never visible to it
        world.AddComponent<Position>(entity); // tick 2

        world.AdvanceTick();
        world.GetComponent<Position>(entity).X = 2f; // tick 3
        using var fastConsumer = world.RegisterChangeConsumer<Position>(); // starts at tick 3, has "caught up"

        world.AdvanceTick();
        world.GetComponent<Position>(entity).X = 3f; // tick 4

        var slowSeenTicks = new List<int>();
        foreach (var entry in slowConsumer.ReadChanges())
            slowSeenTicks.Add(entry.Tick);

        var fastSeenTicks = new List<int>();
        foreach (var entry in fastConsumer.ReadChanges())
            fastSeenTicks.Add(entry.Tick);

        slowSeenTicks.Should().Equal(2, 3, 4);
        fastSeenTicks.Should().Equal(4);
    }

    [Fact]
    public void ReadChanges_OnADisposedConsumer_Throws()
    {
        var world = new World();
        var consumer = world.RegisterChangeConsumer<Position>();
        consumer.Dispose();

        Action act = () => consumer.ReadChanges();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Advance_OnADisposedConsumer_Throws()
    {
        var world = new World();
        var consumer = world.RegisterChangeConsumer<Position>();
        consumer.Dispose();

        var act = () => consumer.Advance(world.CurrentTick);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Advance_ToATickBeforeTheCurrentPosition_Throws()
    {
        var world = new World();
        world.AdvanceTick();
        using var consumer = world.RegisterChangeConsumer<Position>();

        var act = () => consumer.Advance(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Advance_PastTheWorldsCurrentTick_Throws()
    {
        var world = new World();
        using var consumer = world.RegisterChangeConsumer<Position>();

        var act = () => consumer.Advance(world.CurrentTick + 1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
