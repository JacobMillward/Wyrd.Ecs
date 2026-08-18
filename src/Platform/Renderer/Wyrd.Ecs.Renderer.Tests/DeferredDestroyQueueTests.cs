namespace Wyrd.Ecs.Renderer.Tests;

public class DeferredDestroyQueueTests
{
    [Fact]
    public void DrainReady_DoesNotReleaseBeforeFramesInFlightHaveElapsed()
    {
        var queue = new DeferredDestroyQueue();
        var released = false;

        queue.Enqueue(currentFrame: 10, () => released = true);
        queue.DrainReady(currentFrame: 12, framesInFlight: 3);

        released.Should().BeFalse();
    }

    [Fact]
    public void DrainReady_ReleasesOnceFramesInFlightHaveElapsed()
    {
        var queue = new DeferredDestroyQueue();
        var released = false;

        queue.Enqueue(currentFrame: 10, () => released = true);
        queue.DrainReady(currentFrame: 13, framesInFlight: 3);

        released.Should().BeTrue();
    }

    [Fact]
    public void DrainReady_OnlyReleasesEachEntryOnce()
    {
        var queue = new DeferredDestroyQueue();
        var releaseCount = 0;

        queue.Enqueue(currentFrame: 10, () => releaseCount++);
        queue.DrainReady(currentFrame: 20, framesInFlight: 3);
        queue.DrainReady(currentFrame: 30, framesInFlight: 3);

        releaseCount.Should().Be(1);
    }

    [Fact]
    public void DrainReady_ReleasesMultipleEntriesInEnqueueOrder()
    {
        var queue = new DeferredDestroyQueue();
        var order = new List<int>();

        queue.Enqueue(currentFrame: 1, () => order.Add(1));
        queue.Enqueue(currentFrame: 2, () => order.Add(2));
        queue.DrainReady(currentFrame: 10, framesInFlight: 3);

        order.Should().Equal(1, 2);
    }
}
