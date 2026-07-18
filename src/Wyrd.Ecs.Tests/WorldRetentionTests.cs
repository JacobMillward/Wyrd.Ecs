namespace Wyrd.Ecs.Tests;

public class WorldRetentionTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    [Fact]
    public void SingleConsumer_TrimsEverythingItHasAdvancedPast()
    {
        var world = new World();
        using var consumer = world.RegisterChangeConsumer<Position>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity); // tick 1

        world.AdvanceTick(); // tick 2
        world.GetComponent<Position>(entity).X = 2f;

        consumer.Advance(world.CurrentTick);
        world.AdvanceTick(); // triggers the trim for everything <= tick 2

        var seenTicks = new List<int>();
        foreach (var entry in consumer.ReadChanges())
            seenTicks.Add(entry.Tick);

        seenTicks.Should().BeEmpty(); // this consumer has already advanced past everything trimmed
    }

    [Fact]
    public void SlowestConsumer_BlocksTrimmingPastItsOwnPosition()
    {
        var world = new World();
        using var slow = world.RegisterChangeConsumer<Position>();
        world.AdvanceTick(); // entries recorded in the same tick as registration are never visible to it
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity); // tick 2

        world.AdvanceTick(); // tick 3
        world.GetComponent<Position>(entity).X = 2f;
        using var fast = world.RegisterChangeConsumer<Position>(); // starts at tick 3

        world.AdvanceTick(); // tick 4
        world.GetComponent<Position>(entity).X = 3f;

        fast.Advance(world.CurrentTick); // fast has caught up to tick 4, slow is still at its registration tick (1)
        world.AdvanceTick(); // trims only what's <= the minimum across consumers (1, from `slow`), i.e. nothing

        var slowSeenTicks = new List<int>();
        foreach (var entry in slow.ReadChanges())
            slowSeenTicks.Add(entry.Tick);

        slowSeenTicks.Should().Equal(2, 3, 4); // nothing was trimmed, because `slow` never advanced
    }

    [Fact]
    public void NoRegisteredConsumer_NeverTrimsBecauseNeverAppends()
    {
        var world = new World();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        world.AdvanceTick();
        world.AdvanceTick();
        world.AdvanceTick();

        // Register only now; if retention had run against an untracked type there
        // would be nothing recorded to see regardless, proving the type never appended.
        using var consumer = world.RegisterChangeConsumer<Position>();
        var seen = new List<Entity>();
        foreach (var entry in consumer.ReadChanges())
            seen.Add(entry.Entity);

        seen.Should().BeEmpty();
    }

    [Fact]
    public void LargeBacklog_TrimThenContinuedGrowth_StaysConsistent()
    {
        var world = new World();
        using var consumer = world.RegisterChangeConsumer<Position>();

        for (var i = 0; i < 50; i++)
        {
            var entity = world.CreateEntity();
            world.AddComponent<Position>(entity); // logs ticks 1..50
            world.AdvanceTick();
        }

        consumer.Advance(25);
        world.AdvanceTick(); // trims everything <= 25, leaving ticks 26..50 live

        for (var i = 0; i < 50; i++)
        {
            var entity = world.CreateEntity();
            world.AddComponent<Position>(entity); // logs ticks 52..101, past the log's original capacity
            world.AdvanceTick(); // consumer never advances again, so retention should skip every one of these
        }

        var seenTicks = new List<int>();
        foreach (var entry in consumer.ReadChanges())
            seenTicks.Add(entry.Tick);

        seenTicks.Should().HaveCount(75); // (50 - 25) untrimmed from the first batch + all 50 from the second
        seenTicks.Should().BeInAscendingOrder();
        seenTicks.Should().OnlyHaveUniqueItems();
        seenTicks.Should().OnlyContain(tick => tick > 25);
    }

    [Fact]
    public void DisposingTheOnlyConsumer_TurnsTrackingBackOff()
    {
        var world = new World();
        var consumer = world.RegisterChangeConsumer<Position>();
        var entity = world.CreateEntity();
        world.AddComponent<Position>(entity);
        consumer.Dispose();

        world.AdvanceTick();
        world.GetComponent<Position>(entity).X = 5f;

        using var newConsumer = world.RegisterChangeConsumer<Position>();
        var seen = new List<Entity>();
        foreach (var entry in newConsumer.ReadChanges())
            seen.Add(entry.Entity);

        seen.Should().BeEmpty(); // registered after the mutation above, and tracking was off during it anyway
    }
}
