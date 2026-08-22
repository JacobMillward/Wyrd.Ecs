using BenchmarkDotNet.Attributes;
using System.Threading;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

public struct EvtA : IEvent { public int Value; }
public struct EvtB : IEvent { public int Value; }
public struct EvtC : IEvent { public int Value; }
public struct EvtD : IEvent { public int Value; }

/// <summary>
/// Measures <see cref="World.Emit{T}"/> throughput: once per frame in input/collision-style
/// workloads, so the per-emit constant matters. SameChannel isolates contention on one
/// event type's own lock; MixedTypes isolates contention on state shared across all event
/// types (the world-wide channel registry), which unrelated event types should never
/// contend on. Each invocation emits <see cref="EmitsPerThread"/> per thread; returned sinks
/// defeat dead-code elimination.
/// </summary>
[MemoryDiagnoser]
public class EmitThroughputBenchmarks
{
    public const int EmitsPerThread = 8192;

    private World _world = null!;

    [GlobalSetup]
    public void Setup()
    {
        _world = new World();
        // Pre-create all channels so measured runs exercise only the emit steady state.
        _world.Emit(new EvtA());
        _world.Emit(new EvtB());
        _world.Emit(new EvtC());
        _world.Emit(new EvtD());
    }

    private static void RunThreads(Action<int>[] bodies)
    {
        var barrier = new Barrier(bodies.Length);
        var threads = new Thread[bodies.Length];
        for (var i = 0; i < bodies.Length; i++)
        {
            var body = bodies[i];
            threads[i] = new Thread(() =>
            {
                barrier.SignalAndWait();
                body(0);
            });
        }

        foreach (var thread in threads) thread.Start();
        foreach (var thread in threads) thread.Join();
    }

    [Benchmark(Baseline = true)]
    public long SingleThreaded()
    {
        var sink = 0L;
        for (var i = 0; i < EmitsPerThread; i++)
        {
            _world.Emit(new EvtA { Value = i });
            sink += i;
        }

        return sink;
    }

    [Benchmark]
    public long FourThreads_SameChannel()
    {
        var sink = 0L;
        RunThreads([
            _ => { for (var i = 0; i < EmitsPerThread; i++) { _world.Emit(new EvtA { Value = i }); sink += i; } },
            _ => { for (var i = 0; i < EmitsPerThread; i++) { _world.Emit(new EvtA { Value = i }); sink += i; } },
            _ => { for (var i = 0; i < EmitsPerThread; i++) { _world.Emit(new EvtA { Value = i }); sink += i; } },
            _ => { for (var i = 0; i < EmitsPerThread; i++) { _world.Emit(new EvtA { Value = i }); sink += i; } },
        ]);
        return sink;
    }

    [Benchmark]
    public long FourThreads_MixedTypes()
    {
        var sink = 0L;
        RunThreads([
            _ => { for (var i = 0; i < EmitsPerThread; i++) { _world.Emit(new EvtA { Value = i }); sink += i; } },
            _ => { for (var i = 0; i < EmitsPerThread; i++) { _world.Emit(new EvtB { Value = i }); sink += i; } },
            _ => { for (var i = 0; i < EmitsPerThread; i++) { _world.Emit(new EvtC { Value = i }); sink += i; } },
            _ => { for (var i = 0; i < EmitsPerThread; i++) { _world.Emit(new EvtD { Value = i }); sink += i; } },
        ]);
        return sink;
    }
}
