using BenchmarkDotNet.Attributes;
using Wyrd.Ecs.Persistence;
using Wyrd.Ecs.Persistence.Continuous;

namespace Wyrd.Ecs.Benchmarks.Wyrd;

/// <summary>
/// Measures <c>ChangeCapture</c>'s tick-driven capture cost, the work that runs
/// synchronously inside <c>World.AdvanceTick</c> whenever continuous persistence is
/// enabled, at the engine's ~20,000-entity target scale against a 16.6ms/60Hz frame budget.
/// </summary>
[MemoryDiagnoser]
public class ContinuousPersistenceTickBenchmarks
{
    private struct Position : IComponent
    {
        public float X;
        public float Y;
    }

    [Params(1_000, 20_000)]
    public int EntityCount { get; set; }

    private string _directory = null!;
    private World _world = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"wyrd-benchmarks-continuous-tick-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);

        var registry = new ComponentCodecRegistry();
        registry.Register<Position>("Position", p => BitConverter.GetBytes(p.X), bytes => new Position { X = BitConverter.ToSingle(bytes) });

        _world = new WorldBuilder()
            .SetDefaultComponentCodecRegistry(registry)
            .SetDefaultPersistenceStore(new FileStore(Path.Combine(_directory, "world.checkpoint")))
            .EnableContinuousPersistence(
                new FileWalStore(Path.Combine(_directory, "world")),
                options: new WalOptions { FsyncInterval = TimeSpan.FromHours(1), CheckpointInterval = TimeSpan.FromHours(1) })
            .Build();

        for (var i = 0; i < EntityCount; i++)
            _world.Commands.CreateEntity(new Position { X = i, Y = 1f });
        _world.ApplyCommands();
        _world.AdvanceTick();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _world.StopContinuousPersistence(mergeFinalCheckpoint: false);
        Directory.Delete(_directory, recursive: true);
    }

    /// <summary>
    /// Every entity's Position changes, then one AdvanceTick runs the full deferred
    /// capture cost (every registered type's changed-row scan plus a box per changed
    /// value, no encoding) at EntityCount scale.
    /// </summary>
    [Benchmark]
    public void AdvanceTick_AfterEveryEntityChanged()
    {
        _world.Query<Mut<Position>>(chunk =>
        {
            for (var i = 0; i < chunk.Length; i++)
                chunk[i].X += 1f;
        });

        _world.AdvanceTick();
    }
}
