using BenchmarkDotNet.Attributes;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Quantifies the deferred cost of lazy permanent-id assignment: every entity's first
/// <see cref="World.GetPermanentId"/> read pays the one-time random mint; subsequent reads
/// are plain array lookups. Spawn-side savings are covered by the EntityLifecycle suite.
/// </summary>
[MemoryDiagnoser]
public class PermanentIdBenchmarks
{
    public const int EntityCount = 10_000;

    private World _world = null!;
    private Entity[] _entities = null!;

    [IterationSetup]
    public void Setup()
    {
        _world = new World();
        _entities = new Entity[EntityCount];
        for (var i = 0; i < EntityCount; i++)
            _entities[i] = _world.Commands.CreateEntity();
        _world.ApplyCommands();
    }

    /// <summary>Every entity's first permanent-id read: includes the one-time random mint.</summary>
    [Benchmark]
    public long FirstTouchPermanentIds()
    {
        var sink = 0L;
        for (var i = 0; i < EntityCount; i++)
            sink += (long)_world.GetPermanentId(_entities[i]).Value.GetHashCode();
        return sink;
    }
}
