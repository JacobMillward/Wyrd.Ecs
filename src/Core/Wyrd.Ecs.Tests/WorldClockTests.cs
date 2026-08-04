namespace Wyrd.Ecs.Tests;

public class WorldClockTests
{
    [Fact]
    public void RealTime_AdvancesByRawDeltaRegardlessOfPauseOrScale()
    {
        var world = new World();
        world.Pause();
        world.TimeScale = 0.5;

        world.Update(TimeSpan.FromSeconds(1));
        world.Update(TimeSpan.FromSeconds(2));

        world.RealTime.Elapsed.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void TimeScale_DefaultsToOne()
    {
        var world = new World();

        world.TimeScale.Should().Be(1.0);
    }

    [Fact]
    public void TimeScale_Negative_Throws()
    {
        var world = new World();

        var act = () => world.TimeScale = -0.1;

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PauseThenResume_RestoresThePreviouslySetTimeScale()
    {
        var world = new World();
        world.TimeScale = 2.0;

        world.Pause();
        world.IsPaused.Should().BeTrue();
        world.TimeScale.Should().Be(2.0, "Pause must not read or write TimeScale's stored value");

        world.Resume();

        world.IsPaused.Should().BeFalse();
        world.TimeScale.Should().Be(2.0);
    }

    [Fact]
    public void ConcurrentPauseAndTimeScaleWrites_FromMultipleThreads_NeverThrowOrCorruptState()
    {
        var world = new World();

        Parallel.For(0, 1000, i =>
        {
            if (i % 2 == 0) world.Pause(); else world.Resume();
            world.TimeScale = 1.0 + (i % 5);
        });

        // No assertion on the final value (racy by design) — the only contract under test
        // is that concurrent access never throws and TimeScale always reads back one of the
        // values actually written, never a torn/corrupted one.
        new[] { 1.0, 2.0, 3.0, 4.0, 5.0 }.Should().Contain(world.TimeScale);
    }
}
