using BenchmarkDotNet.Attributes;
using Wyrd.Ecs.Persistence;
using Wyrd.Ecs.Persistence.Continuous;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Confirms relation-edge capture cost is per-edge (O(1)), not proportional to a hub
/// entity's existing edge count: link/unlink events flow through
/// <see cref="IStructuralChangeObserver"/> as a per-edge delta, never a whole
/// <c>RelationLinks{T}</c> re-encode. Cost at <c>ExistingEdgeCount</c> 0 and 10,000 should
/// be about the same; a regression would show cost growing with <c>ExistingEdgeCount</c>.
/// </summary>
[MemoryDiagnoser]
public class RelationCaptureBenchmarks
{
    private struct Likes : IRelation
    {
        public float Weight;
    }

    [Params(0, 10_000)]
    public int ExistingEdgeCount { get; set; }

    private string _directory = null!;
    private World _world = null!;
    private Entity _hub;
    private Entity _newTarget;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"wyrd-benchmarks-relation-capture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);

        var registry = new ComponentCodecRegistry();
        registry.RegisterRelation<Likes>("Likes", v => BitConverter.GetBytes(v.Weight), d => new Likes { Weight = BitConverter.ToSingle(d) });

        _world = new WorldBuilder()
            .SetDefaultComponentCodecRegistry(registry)
            .SetDefaultPersistenceStore(new FileStore(Path.Combine(_directory, "world.checkpoint")))
            .EnableContinuousPersistence(
                new FileWalStore(Path.Combine(_directory, "world")),
                options: new WalOptions { FsyncInterval = TimeSpan.FromHours(1), CheckpointInterval = TimeSpan.FromHours(1) })
            .Build();

        _hub = _world.Commands.CreateEntity();
        var existingTargets = new List<Entity>(ExistingEdgeCount);
        for (var i = 0; i < ExistingEdgeCount; i++)
            existingTargets.Add(_world.Commands.CreateEntity());
        _world.ApplyCommands();
        foreach (var target in existingTargets)
            _world.Commands.AddRelation(_hub, target, new Likes { Weight = 1f });
        _newTarget = _world.Commands.CreateEntity();
        _world.ApplyCommands();
        _world.AdvanceTick();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _world.StopContinuousPersistence(mergeFinalCheckpoint: false);
        Directory.Delete(_directory, recursive: true);
    }

    /// <summary>Adds one more edge to a hub entity that already has <see cref="ExistingEdgeCount"/> edges, with continuous persistence's capture pipeline live.</summary>
    [Benchmark]
    public void AddRelation_OneMoreEdgeOnAHighFanoutHub()
    {
        _world.Commands.AddRelation(_hub, _newTarget, new Likes { Weight = 1f });
        _world.ApplyCommands();
    }
}
