namespace Wyrd.Ecs.Tests;

public class WorldDirtyReadTests
{
    internal struct Position : IComponent
    {
        public float X;
    }

    internal struct Velocity : IComponent;

    [Fact]
    public void ReadChanges_SinceTheCurrentTick_DoesNotSeeAnEarlierWrite()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity(new Position()).Entity;
        world.ApplyCommands();
        world.AdvanceTick();

        using var tracking = world.TrackChanges<Position>();
        var sinceTick = world.CurrentTick;

        var seen = new List<Entity>();
        foreach (var change in world.ReadChanges<Position>(sinceTick))
            seen.Add(change.Entity);

        seen.Should().BeEmpty();
    }

    [Fact]
    public void ReadChanges_WithALaterWatermark_SkipsEarlierEntries()
    {
        var world = new World();
        using var tracking = world.TrackChanges<Position>();
        var entity = world.Commands.CreateEntity(new Position()).Entity; // tick 1
        world.ApplyCommands();
        var afterFirstWrite = world.CurrentTick;

        world.AdvanceTick();
        world.GetComponent<Position>(entity).X = 2f; // tick 2

        var seenTicks = new List<int>();
        foreach (var change in world.ReadChanges<Position>(afterFirstWrite))
            seenTicks.Add(change.Tick);

        seenTicks.Should().Equal(2);
    }

    [Fact]
    public void ReadChanges_SpansMultipleArchetypes()
    {
        var world = new World();
        using var tracking = world.TrackChanges<Position>();
        var sinceTick = world.CurrentTick;
        world.AdvanceTick(); // entries recorded at or before sinceTick are never visible

        var onlyPosition = world.Commands.CreateEntity(new Position()).Entity;
        var withVelocity = world.Commands.CreateEntity(new Position(), new Velocity()).Entity;
        world.ApplyCommands();

        var seen = new List<Entity>();
        foreach (var change in world.ReadChanges<Position>(sinceTick))
            seen.Add(change.Entity);

        seen.Should().BeEquivalentTo(new[] { onlyPosition, withVelocity });
    }

    [Fact]
    public void ReadChanges_ReflectsChunkQueryMutations()
    {
        var world = new World();
        using var tracking = world.TrackChanges<Position>();
        var entity = world.Commands.CreateEntity(new Position()).Entity;
        world.ApplyCommands();
        var sinceTick = world.CurrentTick;
        world.AdvanceTick();

        world.Query<Mut<Position>>(chunk => { chunk[0].X += 0f; });

        var seen = new List<Entity>();
        foreach (var change in world.ReadChanges<Position>(sinceTick))
            seen.Add(change.Entity);

        seen.Should().Contain(entity);
    }

    [Fact]
    public void ReadChanges_ReflectsHiddenChunkQueryMutations()
    {
        var world = new World();
        using var tracking = world.TrackChanges<Position>();
        var entity = world.Commands.CreateEntity(new Position()).Entity;
        world.ApplyCommands();
        var sinceTick = world.CurrentTick;
        world.AdvanceTick();

        foreach (var chunk in ArchetypeQuery.Empty.Access<Mut<Position>>().Resolve(world))
        {
            var positions = chunk.Access<Mut<Position>>();
            for (var i = 0; i < chunk.Count; i++)
                positions[i].X += 1f;
        }

        var seen = new List<Entity>();
        foreach (var change in world.ReadChanges<Position>(sinceTick))
            seen.Add(change.Entity);

        seen.Should().Contain(entity);
    }

    [Fact]
    public void TwoIndependentWatermarks_BothSeeTheSameChangeWithoutInterference()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().Entity;
        world.ApplyCommands();

        using var tracking = world.TrackChanges<Position>();
        var sinceTick = world.CurrentTick;
        world.AdvanceTick();
        world.Commands.AddComponent(entity, new Position());
        world.ApplyCommands();

        var firstSeen = new List<Entity>();
        foreach (var change in world.ReadChanges<Position>(sinceTick))
            firstSeen.Add(change.Entity);

        var secondSeen = new List<Entity>();
        foreach (var change in world.ReadChanges<Position>(sinceTick))
            secondSeen.Add(change.Entity);

        firstSeen.Should().Equal(entity);
        secondSeen.Should().Equal(entity);
    }

    [Fact]
    public void ReadChanges_AtDifferentWatermarks_BothSeeTheRowsCurrentTickAndValue()
    {
        var world = new World();
        var entity = world.Commands.CreateEntity().Entity;
        world.ApplyCommands();

        using var tracking = world.TrackChanges<Position>();
        var slowWatermark = world.CurrentTick;
        world.AdvanceTick();
        world.Commands.AddComponent(entity, new Position());
        world.ApplyCommands(); // tick 2

        world.AdvanceTick();
        world.GetComponent<Position>(entity).X = 2f; // tick 3
        var fastWatermark = world.CurrentTick; // caught up to tick 3

        world.AdvanceTick();
        world.GetComponent<Position>(entity).X = 3f; // tick 4 — the row's only current tick-stamp

        var slowSeenTicks = new List<int>();
        foreach (var change in world.ReadChanges<Position>(slowWatermark))
            slowSeenTicks.Add(change.Tick);

        var fastSeenTicks = new List<int>();
        foreach (var change in world.ReadChanges<Position>(fastWatermark))
            fastSeenTicks.Add(change.Tick);

        // There is one current tick-stamp per row, not a log of every past touch — both
        // watermarks see the same single entry, at the row's latest tick.
        slowSeenTicks.Should().Equal(4);
        fastSeenTicks.Should().Equal(4);
    }

    [Fact]
    public void ReadChanges_CalledTwiceWithTheSameWatermark_ReturnsTheSameResultBothTimes()
    {
        var world = new World();
        using var tracking = world.TrackChanges<Position>();
        var sinceTick = world.CurrentTick;
        world.AdvanceTick();
        var entity = world.Commands.CreateEntity(new Position()).Entity;
        world.ApplyCommands();

        var first = new List<Entity>();
        foreach (var change in world.ReadChanges<Position>(sinceTick))
            first.Add(change.Entity);

        var second = new List<Entity>();
        foreach (var change in world.ReadChanges<Position>(sinceTick))
            second.Add(change.Entity);

        first.Should().Equal(entity);
        second.Should().Equal(entity);
    }
}
