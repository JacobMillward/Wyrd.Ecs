using BenchmarkDotNet.Attributes;
using Wyrd.Ecs.Persistence;
using Wyrd.Ecs.Persistence.Continuous;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Relation-edge analog of <see cref="CheckpointBuildBenchmarks"/>: measures the merge
/// cost when the prior checkpoint's working set is dominated by relation-edge records
/// (one hub entity with many edges) rather than component records.
/// </summary>
[MemoryDiagnoser]
public class CheckpointBuildRelationBenchmarks
{
    private struct Likes : IRelation
    {
        public float Weight;
    }

    [Params(1_000, 20_000)]
    public int EdgeCount { get; set; }

    private string _directory = null!;
    private IPersistenceStore _checkpointStore = null!;
    private IWalStore _walStore = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"wyrd-benchmarks-checkpoint-build-relation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);

        var registry = new ComponentCodecRegistry();
        registry.RegisterRelation<Likes>("Likes", v => BitConverter.GetBytes(v.Weight), d => new Likes { Weight = BitConverter.ToSingle(d) });

        var world = new World();
        world.DefaultComponentCodecRegistry = registry;
        var hub = world.Commands.CreateEntity();
        var targets = new List<Entity>(EdgeCount);
        for (var i = 0; i < EdgeCount; i++)
            targets.Add(world.Commands.CreateEntity());
        world.ApplyCommands();
        foreach (var target in targets)
            world.Commands.AddRelation(hub, target, new Likes { Weight = 1f });
        world.ApplyCommands();

        _checkpointStore = new FileStore(Path.Combine(_directory, "world.checkpoint"));
        world.Save(_checkpointStore);
        _walStore = new FileWalStore(Path.Combine(_directory, "world"));
    }

    [GlobalCleanup]
    public void GlobalCleanup() => Directory.Delete(_directory, recursive: true);

    /// <summary>
    /// The cheapest real call shape (no WAL records to apply) still has to read every
    /// relation edge in the prior checkpoint back into memory and rewrite it. This is
    /// that cost, at EdgeCount scale.
    /// </summary>
    [Benchmark]
    public void Build_WithHighFanoutRelationEdgesAndNoWalRecordsToApply()
    {
        CheckpointBuilder.Build(_checkpointStore, _walStore, targetTick: 1);
    }
}
