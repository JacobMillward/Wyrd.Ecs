using System.Text;
using Wyrd.Ecs.Persistence.Continuous.Internal;

namespace Wyrd.Ecs.Persistence.Continuous.Tests;

public class WorldContinuousPersistenceExtensionsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"wyrd-continuous-worldbuilder-{Guid.NewGuid():N}");
    private string CheckpointPath => Path.Combine(_directory, "world.checkpoint");
    private string WalBasePath => Path.Combine(_directory, "world");

    private struct Position : IComponent
    {
        public float X;
    }

    private struct Velocity : IComponent
    {
        public float X;
    }

    public WorldContinuousPersistenceExtensionsTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static ComponentCodecRegistry BuildRegistry()
    {
        var registry = new ComponentCodecRegistry();
        registry.Register<Position>("Position",
            p => Encoding.UTF8.GetBytes(p.X.ToString()),
            bytes => new Position { X = float.Parse(Encoding.UTF8.GetString(bytes)) });
        registry.Register<Velocity>("Velocity",
            v => Encoding.UTF8.GetBytes(v.X.ToString()),
            bytes => new Velocity { X = float.Parse(Encoding.UTF8.GetString(bytes)) });
        return registry;
    }

    [Fact]
    public void EnableContinuousPersistence_WithoutADefaultRegistry_ThrowsWhenBuilt()
    {
        var options = new ContinuousOptions { CheckpointStore = new FileStore(CheckpointPath), WalStore = new FileWalStore(WalBasePath) };
        var builder = new WorldBuilder().EnableContinuousPersistence(options);

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void EnableContinuousPersistence_WritesABootstrapCheckpointImmediately()
    {
        var registry = BuildRegistry();
        var checkpointStore = new FileStore(CheckpointPath);
        var options = new ContinuousOptions { CheckpointStore = checkpointStore, WalStore = new FileWalStore(WalBasePath) };

        var world = new WorldBuilder()
            .SetDefaultComponentCodecRegistry(registry)
            .EnableContinuousPersistence(options)
            .Build();

        var (tick, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        // One less than CurrentTick: EnableContinuousPersistence advances the tick once
        // after taking the bootstrap snapshot, sealing its boundary (see the
        // implementation's comment for why).
        tick.Should().Be(world.CurrentTick - 1);
        world.StopContinuousPersistence();
    }

    [Fact]
    public void EnableContinuousPersistence_OpensAWalSegmentImmediately()
    {
        var registry = BuildRegistry();
        var walStore = new FileWalStore(WalBasePath);
        var options = new ContinuousOptions { CheckpointStore = new FileStore(CheckpointPath), WalStore = walStore };

        var world = new WorldBuilder()
            .SetDefaultComponentCodecRegistry(registry)
            .EnableContinuousPersistence(options)
            .Build();

        walStore.ListSegmentStartTicks().Should().ContainSingle();
        world.StopContinuousPersistence();
    }

    [Fact]
    public void StopContinuousPersistence_WithoutEnabling_Throws()
    {
        var world = new World();

        var act = () => world.StopContinuousPersistence();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void StopContinuousPersistence_StopsFurtherWalWrites()
    {
        var registry = BuildRegistry();
        var walStore = new FileWalStore(WalBasePath);
        var options = new ContinuousOptions
        {
            CheckpointStore = new FileStore(CheckpointPath),
            WalStore = walStore,
            Options = new WalOptions { FsyncInterval = TimeSpan.FromMilliseconds(20), CheckpointInterval = TimeSpan.FromMinutes(10) },
        };
        var world = new WorldBuilder().SetDefaultComponentCodecRegistry(registry).EnableContinuousPersistence(options).Build();

        world.StopContinuousPersistence();
        var segmentTickAtStop = walStore.ListSegmentStartTicks().Single();

        var entity = world.Commands.CreateEntity(new Position { X = 1f });
        world.ApplyCommands();
        world.AdvanceTick();
        Thread.Sleep(100); // give a hypothetical still-running thread a chance to misbehave

        using var readStream = walStore.OpenSegmentRead(segmentTickAtStop);
        WalSegmentIO.ReadHeader(readStream);
        var found = false;
        while (WalSegmentIO.TryReadRecord(readStream, out _, out _, out var readEntity, out _, out _, out _))
            found = found || readEntity == world.GetPermanentId(entity);
        found.Should().BeFalse();
    }

    [Fact]
    public void EndToEnd_MutationsAcrossSeveralTicksSurviveStopAndReload()
    {
        var registry = BuildRegistry();
        var checkpointStore = new FileStore(CheckpointPath);
        var walStore = new FileWalStore(WalBasePath);
        var options = new ContinuousOptions
        {
            CheckpointStore = checkpointStore,
            WalStore = walStore,
            Options = new WalOptions { FsyncInterval = TimeSpan.FromMilliseconds(10), CheckpointInterval = TimeSpan.FromMinutes(10) },
        };
        var world = new WorldBuilder().SetDefaultComponentCodecRegistry(registry).EnableContinuousPersistence(options).Build();

        var surviving = world.Commands.CreateEntity(new Position { X = 1f });
        var destroyed = world.Commands.CreateEntity(new Position { X = 2f });
        world.ApplyCommands();
        world.AdvanceTick();

        world.Commands.AddComponent(surviving, new Velocity { X = 3f });
        world.Commands.DestroyEntity(destroyed);
        world.ApplyCommands();
        world.AdvanceTick();

        world.Commands.RemoveComponent<Velocity>(surviving);
        world.ApplyCommands();
        world.AdvanceTick();

        world.StopContinuousPersistence();
        // Fold whatever's left in the WAL into the checkpoint, exactly as real
        // recovery would after a crash — WorldSnapshot.Load only reads the checkpoint
        // store, so reload requires this same composition (merge, then load), not a
        // new primitive.
        CheckpointBuilder.Build(checkpointStore, walStore, world.CurrentTick);

        var reloaded = new World();
        var reloadedRegistry = BuildRegistry();
        WorldSnapshot.Load(reloaded, reloadedRegistry, checkpointStore);

        var positions = new List<float>();
        foreach (var row in reloaded.Query<Position>())
            positions.Add(row.Get<Position>().X);
        positions.Should().Equal(1f); // only the surviving entity, at its original value — the destroyed one is gone

        var velocityCount = 0;
        foreach (var row in reloaded.Query<Velocity>())
            velocityCount++;
        velocityCount.Should().Be(0); // added then removed — must not resurrect
    }
}
