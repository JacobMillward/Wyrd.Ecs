namespace Wyrd.Ecs.Renderer.Tests;

public class FrameInFlightTrackerTests
{
    [Fact]
    public void CurrentFrame_StartsAtZero()
    {
        var tracker = new FrameInFlightTracker();

        tracker.CurrentFrame.Should().Be(0);
        tracker.SlotIndex.Should().Be(0);
    }

    [Fact]
    public void Advance_IncrementsCurrentFrame()
    {
        var tracker = new FrameInFlightTracker();

        tracker.Advance();
        tracker.Advance();

        tracker.CurrentFrame.Should().Be(2);
    }

    [Fact]
    public void SlotIndex_WrapsAtFramesInFlight()
    {
        var tracker = new FrameInFlightTracker();

        var slots = new List<int> { tracker.SlotIndex };
        for (var i = 0; i < 5; i++)
        {
            tracker.Advance();
            slots.Add(tracker.SlotIndex);
        }

        slots.Should().Equal(0, 1, 2, 0, 1, 2);
    }
}
