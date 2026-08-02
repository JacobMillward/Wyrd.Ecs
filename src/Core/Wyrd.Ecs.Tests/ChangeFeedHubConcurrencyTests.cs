using System.Collections.Concurrent;

namespace Wyrd.Ecs.Tests;

public class ChangeFeedHubConcurrencyTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    /// <summary>
    /// Stresses <see cref="ChangeSubscription.Drain"/>'s documented any-thread contract: one
    /// thread advances the tick while others concurrently subscribe, drain, and dispose.
    /// </summary>
    [Fact]
    public void ConcurrentSubscribeDrainDisposeAgainstAConcurrentlyTickingWorld_NeverThrows()
    {
        var world = new World();
        Entity entity = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();

        var exceptions = new ConcurrentBag<Exception>();
        using var stop = new CancellationTokenSource();

        var tickThread = new Thread(() =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                    world.AdvanceTick();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        var subscriberThreads = Enumerable.Range(0, 4).Select(_ => new Thread(() =>
        {
            try
            {
                for (var i = 0; i < 2_000; i++)
                {
                    using var subscription = world.Subscribe<Position>();
                    subscription.Drain();
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        tickThread.Start();
        foreach (var thread in subscriberThreads) thread.Start();
        foreach (var thread in subscriberThreads) thread.Join();
        stop.Cancel();
        tickThread.Join();

        exceptions.Should().BeEmpty();
    }
}
