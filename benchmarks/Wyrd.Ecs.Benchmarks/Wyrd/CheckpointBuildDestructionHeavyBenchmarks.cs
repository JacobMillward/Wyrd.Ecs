using BenchmarkDotNet.Attributes;
using Wyrd.Ecs.Persistence;
using Wyrd.Ecs.Persistence.Continuous;
using Wyrd.Ecs.Persistence.Continuous.Internal;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Isolates CheckpointBuilder.Build's merge cost specifically when a large fraction of
/// the prior checkpoint's entities are destroyed in the WAL window being merged — the
/// scenario the deferred-destroyed-set refactor (replacing an eager byEntity index)
/// targeted. Run once before that refactor and once after (via git checkout, see
/// docs/superpowers/plans/2026-08-02-relation-persistence-checkpoint-builder-merge.md's
/// Task 1) to confirm the predicted complexity improvement.
/// </summary>
[MemoryDiagnoser]
public class CheckpointBuildDestructionHeavyBenchmarks
{
    private struct Position : IComponent
    {
        public float X;
    }

    private const int EntityCount = 20_000;

    private string _directory = null!;
    private IPersistenceStore _checkpointStore = null!;
    private IWalStore _walStore = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"wyrd-benchmarks-checkpoint-build-destroy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);

        var registry = new ComponentCodecRegistry();
        registry.Register<Position>("Position", p => BitConverter.GetBytes(p.X), bytes => new Position { X = BitConverter.ToSingle(bytes) });

        var world = new World();
        world.DefaultComponentCodecRegistry = registry;
        var entities = new List<Entity>(EntityCount);
        for (var i = 0; i < EntityCount; i++)
            entities.Add(world.Commands.CreateEntity(new Position { X = i }));
        world.ApplyCommands();
        var entityIds = entities.Select(world.GetPermanentId).ToList();

        _checkpointStore = new FileStore(Path.Combine(_directory, "world.checkpoint"));
        world.Save(_checkpointStore);
        _walStore = new FileWalStore(Path.Combine(_directory, "world"));

        // Half the entities are destroyed in the WAL window this benchmark merges.
        using var stream = _walStore.OpenSegmentAppend(2);
        WalSegmentIO.WriteHeader(stream);
        for (var i = 0; i < EntityCount / 2; i++)
            WalSegmentIO.WriteRecord(stream, WalRecordKind.EntityDestroyed, tick: 2, entityIds[i], "", null, []);
    }

    [GlobalCleanup]
    public void GlobalCleanup() => Directory.Delete(_directory, recursive: true);

    [Benchmark]
    public void Build_HalfTheEntitiesDestroyedInOneWindow()
    {
        CheckpointBuilder.Build(_checkpointStore, _walStore, targetTick: 2);
    }
}
