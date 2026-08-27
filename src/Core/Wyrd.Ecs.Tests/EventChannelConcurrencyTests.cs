using System.Threading;
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs.Tests;

public struct CncEvtA : IEvent { public int Value; }
public struct CncEvtB : IEvent { public int Value; }
public struct CncEvtC : IEvent { public int Value; }
public struct CncEvtD : IEvent { public int Value; }
public struct CncEvtE : IEvent { public int Value; }
public struct CncEvtF : IEvent { public int Value; }
public struct CncEvtG : IEvent { public int Value; }
public struct CncEvtH : IEvent { public int Value; }

/// <summary>
/// <see cref="World.Emit{T}"/> documents concurrent emission from parallel-stage systems,
/// while <see cref="World.AdvanceTick"/> is public and callable whenever the caller likes -
/// including while such a stage is mid-flight. Channel generation swaps must serialize
/// against appends; these tests pin that. Barrier-coordinated, bounded rounds, no timing waits.
/// </summary>
public class EventChannelConcurrencyTests
{
    private const int WritesPerWriter = 4096;

    [Fact]
    public void ConcurrentWritesAcrossSwaps_AreNeverTornOrDuplicated()
    {
        var channel = new EventChannel<CncEvtA>();
        const int WriterCount = 4;
        const int SwapRounds = 256;

        // Each event carries writerId*WritesPerWriter + sequence. A correct channel delivers
        // every writer's sequence exactly once, in order: a stale-ref swap race makes slots
        // visible through both generations (duplicates) or skips them mid-count-reset (loss).
        var barrier = new Barrier(WriterCount + 1);
        var exceptions = new Exception?[WriterCount];
        var nextExpected = new int[WriterCount];

        var reader = new Thread(() =>
        {
            try
            {
                var destination = new List<CncEvtA>();
                var cursor = 0L;
                barrier.SignalAndWait();

                for (var round = 0; round < SwapRounds * 4 && !WritersDone(nextExpected); round++)
                {
                    cursor = channel.Read(cursor, destination);
                    foreach (var evt in destination)
                    {
                        var writer = evt.Value / WritesPerWriter;
                        var sequence = evt.Value % WritesPerWriter;
                        if (sequence != Interlocked.CompareExchange(ref nextExpected[writer], sequence + 1, sequence))
                            throw new InvalidOperationException($"writer {writer} delivered sequence {sequence}, expected {nextExpected[writer]}");
                    }

                    channel.Swap();
                }

                // Tail drain: writers finished, so plain reads (no further swaps needed -
                // Read covers the newest generation) complete each writer's tail.
                while (!WritersDone(nextExpected))
                {
                    cursor = channel.Read(cursor, destination);
                    foreach (var evt in destination)
                    {
                        var writer = evt.Value / WritesPerWriter;
                        var sequence = evt.Value % WritesPerWriter;
                        if (sequence != Interlocked.CompareExchange(ref nextExpected[writer], sequence + 1, sequence))
                            throw new InvalidOperationException($"tail: writer {writer} delivered {sequence}, expected {nextExpected[writer]}");
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions[0] = ex;
            }
        });

        var writers = new Thread[WriterCount];
        for (var w = 0; w < WriterCount; w++)
        {
            var writerId = w;
            writers[w] = new Thread(() =>
            {
                try
                {
                    barrier.SignalAndWait();
                    for (var i = 0; i < WritesPerWriter; i++)
                        channel.Write(new CncEvtA { Value = writerId * WritesPerWriter + i });
                }
                catch (Exception ex)
                {
                    exceptions[writerId] = ex;
                }
            });
        }

        reader.Start();
        foreach (var writer in writers) writer.Start();
        reader.Join();
        foreach (var writer in writers) writer.Join();

        var failures = exceptions.OfType<Exception>().ToList();
        failures.Should().BeEmpty($"every write delivered exactly once, in order{Describe(failures)}");
        nextExpected.Should().AllBeEquivalentTo(WritesPerWriter, "every writer's full sequence was consumed");
    }

    private static bool WritersDone(int[] nextExpected)
    {
        foreach (var expected in nextExpected)
            if (expected < WritesPerWriter) return false;
        return true;
    }

    [Fact]
    public void AdvanceTick_WhileFirstEmitOfNewTypesRaces_IsSafe()
    {
        var world = new World();
        const int Rounds = 512;

        var emitterExceptions = new Exception?[1];
        var barrier = new Barrier(2);

        var emitter = new Thread(() =>
        {
            try
            {
                barrier.SignalAndWait();
                for (var round = 0; round < Rounds; round++)
                {
                    // First-time emits mutate _activeEventChannels while AdvanceTick walks it.
                    switch (round % 8)
                    {
                        case 0: world.Emit(new CncEvtA()); break;
                        case 1: world.Emit(new CncEvtB()); break;
                        case 2: world.Emit(new CncEvtC()); break;
                        case 3: world.Emit(new CncEvtD()); break;
                        case 4: world.Emit(new CncEvtE()); break;
                        case 5: world.Emit(new CncEvtF()); break;
                        case 6: world.Emit(new CncEvtG()); break;
                        default: world.Emit(new CncEvtH()); break;
                    }
                }
            }
            catch (Exception ex)
            {
                emitterExceptions[0] = ex;
            }
        });

        Exception? tickException = null;
        emitter.Start();
        try
        {
            barrier.SignalAndWait();
            for (var round = 0; round < Rounds; round++)
                world.AdvanceTick();
        }
        catch (Exception ex)
        {
            tickException = ex;
        }

        emitter.Join();

        emitterExceptions.Should().AllBeEquivalentTo((Exception?)null);
        tickException.Should().BeNull("AdvanceTick iterates a snapshot-stable channel set");
    }

    private static string Describe(List<Exception> failures)
        => failures.Count == 0 ? "" : $"; first failure: {failures[0].GetType().Name}: {failures[0].Message}";
}
