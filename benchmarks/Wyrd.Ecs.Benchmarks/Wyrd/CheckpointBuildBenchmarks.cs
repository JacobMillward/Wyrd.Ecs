using BenchmarkDotNet.Attributes;
using Wyrd.Ecs.Persistence;
using Wyrd.Ecs.Persistence.Continuous;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Measures <c>CheckpointBuilder.Build</c>'s full-rewrite cost at the engine's
/// ~20,000-entity target scale, checked against the default 60-second/64MB checkpoint
/// cadence rather than a per-frame budget, since it runs on its own background thread.
/// </summary>
[MemoryDiagnoser]
public class CheckpointBuildBenchmarks
{
    private struct Position : IComponent
    {
        public float X;
    }

    [Params(1_000, 20_000)]
    public int EntityCount { get; set; }

    private string _directory = null!;
    private IPersistenceStore _checkpointStore = null!;
    private IWalStore _walStore = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"wyrd-benchmarks-checkpoint-build-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);

        var registry = new CodecRegistry();
        registry.Register<Position>("Position", p => BitConverter.GetBytes(p.X), bytes => new Position { X = BitConverter.ToSingle(bytes) });

        var world = new World();
        world.CodecRegistry = registry;
        for (var i = 0; i < EntityCount; i++)
            world.Commands.CreateEntity(new Position { X = i });
        world.ApplyCommands();

        _checkpointStore = new FileStore(Path.Combine(_directory, "world.checkpoint"));
        world.Save(_checkpointStore);
        _walStore = new FileWalStore(Path.Combine(_directory, "world"));
    }

    [GlobalCleanup]
    public void GlobalCleanup() => Directory.Delete(_directory, recursive: true);

    /// <summary>
    /// The cheapest real call shape (no WAL records to apply) still has to read the
    /// whole prior checkpoint back into memory and rewrite it. This is that cost, at
    /// EntityCount scale.
    /// </summary>
    [Benchmark]
    public void Build_WithNoWalRecordsToApply()
    {
        CheckpointBuilder.Build(_checkpointStore, _walStore, targetTick: 1);
    }
}
