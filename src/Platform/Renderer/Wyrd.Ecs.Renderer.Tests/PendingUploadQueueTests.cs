namespace Wyrd.Ecs.Renderer.Tests;

public class PendingUploadQueueTests
{
    [Fact]
    public void DrainInto_InvokesEachEnqueuedActionWithTheGivenHandle()
    {
        var queue = new PendingUploadQueue();
        var received = new List<IntPtr>();
        var fakeCopyPass = new IntPtr(1234);

        queue.Enqueue(handle => received.Add(handle));
        queue.Enqueue(handle => received.Add(handle));
        queue.DrainInto(fakeCopyPass);

        received.Should().Equal(fakeCopyPass, fakeCopyPass);
    }

    [Fact]
    public void DrainInto_LeavesTheQueueEmptyAfterDraining()
    {
        var queue = new PendingUploadQueue();
        var invocationCount = 0;
        queue.Enqueue(_ => invocationCount++);

        queue.DrainInto(new IntPtr(1));
        queue.DrainInto(new IntPtr(2));

        invocationCount.Should().Be(1);
    }

    [Fact]
    public void DrainInto_WithNothingQueued_DoesNotThrow()
    {
        var queue = new PendingUploadQueue();

        var act = () => queue.DrainInto(new IntPtr(1));

        act.Should().NotThrow();
    }
}
