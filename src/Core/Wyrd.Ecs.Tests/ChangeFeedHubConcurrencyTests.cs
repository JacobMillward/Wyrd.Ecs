using System.Collections.Concurrent;

namespace Wyrd.Ecs.Tests;

public class ChangeFeedHubConcurrencyTests
{
    private struct Position : IComponent
    {
        public float X;
    }

    /// <summary>
    /// <see cref="ChangeSubscription.Drain"/> is documented as callable from any
    /// thread — the design's own stated goal is future consumers like a background
    /// WAL-writer thread or a network client subscribing independently of the sim
    /// thread. This stresses exactly that: one thread repeatedly advancing the tick
    /// (mutating the hub's internal scan/watermark state) while several others
    /// concurrently subscribe, drain, and dispose (mutating the hub's subscriber
    /// bookkeeping) — none of it should ever throw. Before the hub's subscriber
    /// dictionaries were lock-protected, this reliably threw
    /// <c>InvalidOperationException</c> ("Collection was modified") within the first
    /// few hundred iterations.
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
