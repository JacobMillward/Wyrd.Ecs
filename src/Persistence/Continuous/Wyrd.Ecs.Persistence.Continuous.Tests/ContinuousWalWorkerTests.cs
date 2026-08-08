using Wyrd.Ecs.Persistence.Continuous.Internal;

namespace Wyrd.Ecs.Persistence.Continuous.Tests;

public class ContinuousWalWorkerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"wyrd-continuous-walworker-{Guid.NewGuid():N}");
    private string CheckpointPath => Path.Combine(_directory, "world.checkpoint");
    private string WalBasePath => Path.Combine(_directory, "world");

    private struct Position : IComponent
    {
        public float X;
    }

    public ContinuousWalWorkerTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private static CodecRegistry BuildRegistry()
    {
        var registry = new CodecRegistry();
        registry.Register<Position>("Position",
            p => BitConverter.GetBytes(p.X),
            bytes => new Position { X = BitConverter.ToSingle(bytes) });
        return registry;
    }

    [Fact]
    public void Constructor_OpensASegmentImmediately()
    {
        var world = new World();
        using var capture = new ChangeCapture(world, BuildRegistry());
        var walStore = new FileWalStore(WalBasePath);

        _ = new ContinuousWalWorker(world, capture, new FileStore(CheckpointPath), walStore, WalOptions.Default);

        walStore.ListSegmentStartTicks().Should().ContainSingle();
    }

    [Fact]
    public void WalWriteCycle_WritesCapturedEntriesToTheOpenSegment()
    {
        var world = new World();
        var registry = BuildRegistry();
        using var capture = new ChangeCapture(world, registry);
        var walStore = new FileWalStore(WalBasePath);
        var worker = new ContinuousWalWorker(world, capture, new FileStore(CheckpointPath), walStore, WalOptions.Default);

        Entity entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();
        world.AdvanceTick();

        worker.WalWriteCycle();

        using var readStream = walStore.OpenSegmentRead(walStore.ListSegmentStartTicks()[0]);
        WalSegmentIO.ReadHeader(readStream);
        var records = new List<(WalRecordKind Kind, EntityId EntityId, string Discriminator)>();
        while (WalSegmentIO.TryReadRecord(readStream, out var record))
            records.Add((record.Kind, record.EntityId, record.Discriminator));

        records.Should().Contain(r => r.Kind == WalRecordKind.EntityCreated && r.EntityId == world.GetPermanentId(entity));
        records.Should().Contain(r => r.Kind == WalRecordKind.ComponentChanged && r.EntityId == world.GetPermanentId(entity) && r.Discriminator == "Position");
    }

    [Fact]
    public void WalWriteCycle_WithNothingCaptured_WritesNoRecordsAndDoesNotThrow()
    {
        var world = new World();
        using var capture = new ChangeCapture(world, BuildRegistry());
        var walStore = new FileWalStore(WalBasePath);
        var worker = new ContinuousWalWorker(world, capture, new FileStore(CheckpointPath), walStore, WalOptions.Default);

        var act = () => worker.WalWriteCycle();

        act.Should().NotThrow();
        using var readStream = walStore.OpenSegmentRead(walStore.ListSegmentStartTicks()[0]);
        WalSegmentIO.ReadHeader(readStream);
        WalSegmentIO.TryReadRecord(readStream, out _).Should().BeFalse();
    }

    [Fact]
    public void WalWriteCycle_WhenFlushingTheWalStoreThrows_ReportsViaTheErrorCallbackAndDoesNotThrow()
    {
        var world = new World();
        using var capture = new ChangeCapture(world, BuildRegistry());
        // Segment opening (in the constructor, outside any try/catch) must succeed; this
        // test breaks Flush instead, which runs every WalWriteCycle.
        var flushThrowingWalStore = new FlushThrowingWalStore(WalBasePath);
        Exception? reported = null;
        var worker = new ContinuousWalWorker(world, capture, new FileStore(CheckpointPath), flushThrowingWalStore, WalOptions.Default, ex => reported = ex);

        var act = () => worker.WalWriteCycle();

        act.Should().NotThrow();
        reported.Should().NotBeNull();
    }

    private sealed class FlushThrowingWalStore(string basePath) : IWalStore
    {
        private readonly FileWalStore _inner = new(basePath);
        public Stream OpenSegmentAppend(int startTick) => _inner.OpenSegmentAppend(startTick);
        public Stream OpenSegmentRead(int startTick) => _inner.OpenSegmentRead(startTick);
        public IReadOnlyList<int> ListSegmentStartTicks() => _inner.ListSegmentStartTicks();
        public void DeleteSegment(int startTick) => _inner.DeleteSegment(startTick);
        public void Flush(Stream segment) => throw new IOException("simulated failure");
    }

    [Fact]
    public void CheckpointMergeCycle_MergesTheRotatedOutSegmentIntoANewCheckpointAndRetiresIt()
    {
        var world = new World();
        var registry = BuildRegistry();
        using var capture = new ChangeCapture(world, registry);
        var walStore = new FileWalStore(WalBasePath);
        var checkpointStore = new FileStore(CheckpointPath);
        var worker = new ContinuousWalWorker(world, capture, checkpointStore, walStore, WalOptions.Default);

        world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();
        world.AdvanceTick();
        worker.WalWriteCycle();
        var initialSegmentTick = walStore.ListSegmentStartTicks().Single();

        // A dedicated Thread, not Task.Run: under xUnit's parallel execution, ThreadPool
        // contention can starve a Task.Run item past this test's polling deadline, causing
        // flaky failures unrelated to the logic under test.
        var mergeThread = new Thread(worker.CheckpointMergeCycle);
        mergeThread.Start();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (mergeThread.IsAlive && DateTime.UtcNow < deadline)
        {
            worker.WalWriteCycle();
            Thread.Sleep(10);
        }
        mergeThread.Join(TimeSpan.FromSeconds(1)).Should().BeTrue();

        walStore.ListSegmentStartTicks().Should().NotContain(initialSegmentTick);
        var (_, entries, _, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        entries.Keys.Should().Contain(k => k.Discriminator == "Position");
    }

    [Fact]
    public void CheckpointMergeCycle_LeavesTheNewlyRotatedSegmentInPlace()
    {
        var world = new World();
        var registry = BuildRegistry();
        using var capture = new ChangeCapture(world, registry);
        var walStore = new FileWalStore(WalBasePath);
        var checkpointStore = new FileStore(CheckpointPath);
        var worker = new ContinuousWalWorker(world, capture, checkpointStore, walStore, WalOptions.Default);

        var mergeThread = new Thread(worker.CheckpointMergeCycle);
        mergeThread.Start();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (mergeThread.IsAlive && DateTime.UtcNow < deadline)
        {
            worker.WalWriteCycle();
            Thread.Sleep(10);
        }
        mergeThread.Join(TimeSpan.FromSeconds(1)).Should().BeTrue();

        walStore.ListSegmentStartTicks().Should().ContainSingle();
    }

    [Fact]
    public void CheckpointMergeCycle_WhenCheckpointBuilderThrows_ReportsViaTheErrorCallbackAndDoesNotThrow()
    {
        var world = new World();
        using var capture = new ChangeCapture(world, BuildRegistry());
        var walStore = new FileWalStore(WalBasePath);
        Exception? reported = null;
        var worker = new ContinuousWalWorker(world, capture, new ThrowingPersistenceStore(), walStore, WalOptions.Default, ex => reported = ex);

        var mergeThread = new Thread(worker.CheckpointMergeCycle);
        mergeThread.Start();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (mergeThread.IsAlive && DateTime.UtcNow < deadline)
        {
            worker.WalWriteCycle();
            Thread.Sleep(10);
        }

        mergeThread.Join(TimeSpan.FromSeconds(1)).Should().BeTrue();
        reported.Should().NotBeNull();
    }

    private sealed class ThrowingPersistenceStore : IPersistenceStore
    {
        public Stream OpenCheckpointWrite() => throw new IOException("simulated failure");
        public Stream OpenCheckpointRead() => throw new IOException("simulated failure");
    }

    [Fact]
    public void Start_WithAShortFsyncInterval_DrainsCapturedChangesOnItsOwn()
    {
        var world = new World();
        var registry = BuildRegistry();
        using var capture = new ChangeCapture(world, registry);
        var walStore = new FileWalStore(WalBasePath);
        var options = new WalOptions { FsyncInterval = TimeSpan.FromMilliseconds(20), CheckpointInterval = TimeSpan.FromMinutes(10) };
        using var worker = new ContinuousWalWorker(world, capture, new FileStore(CheckpointPath), walStore, options);
        worker.Start();

        Entity entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();
        world.AdvanceTick();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        var found = false;
        while (!found && DateTime.UtcNow < deadline)
        {
            using var readStream = walStore.OpenSegmentRead(walStore.ListSegmentStartTicks()[0]);
            WalSegmentIO.ReadHeader(readStream);
            while (WalSegmentIO.TryReadRecord(readStream, out var record))
                found = found || record.EntityId == world.GetPermanentId(entity);
            if (!found) Thread.Sleep(10);
        }

        found.Should().BeTrue();
    }

    [Fact]
    public void Start_WithAShortCheckpointInterval_ProducesAWorkingCheckpointOnItsOwn()
    {
        var world = new World();
        var registry = BuildRegistry();
        using var capture = new ChangeCapture(world, registry);
        var walStore = new FileWalStore(WalBasePath);
        var checkpointStore = new FileStore(CheckpointPath);
        var options = new WalOptions { FsyncInterval = TimeSpan.FromMilliseconds(20), CheckpointInterval = TimeSpan.FromMilliseconds(50) };
        using var worker = new ContinuousWalWorker(world, capture, checkpointStore, walStore, options);
        worker.Start();

        world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();
        world.AdvanceTick();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        var found = false;
        while (!found && DateTime.UtcNow < deadline)
        {
            var (_, entries, _, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
            found = entries.Keys.Any(k => k.Discriminator == "Position");
            if (!found) Thread.Sleep(10);
        }

        found.Should().BeTrue();
    }

    [Fact]
    public void Dispose_WithoutStart_DoesNotThrow()
    {
        var world = new World();
        using var capture = new ChangeCapture(world, BuildRegistry());
        var worker = new ContinuousWalWorker(world, capture, new FileStore(CheckpointPath), new FileWalStore(WalBasePath), WalOptions.Default);

        var act = () => worker.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_AfterStart_DrainsAnyFinalCapturedChangesBeforeStopping()
    {
        var world = new World();
        var registry = BuildRegistry();
        using var capture = new ChangeCapture(world, registry);
        var walStore = new FileWalStore(WalBasePath);
        var options = new WalOptions { FsyncInterval = TimeSpan.FromSeconds(10), CheckpointInterval = TimeSpan.FromMinutes(10) };
        var worker = new ContinuousWalWorker(world, capture, new FileStore(CheckpointPath), walStore, options);
        worker.Start();

        Entity entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();
        world.AdvanceTick();

        worker.Dispose();

        using var readStream = walStore.OpenSegmentRead(walStore.ListSegmentStartTicks()[0]);
        WalSegmentIO.ReadHeader(readStream);
        var found = false;
        while (WalSegmentIO.TryReadRecord(readStream, out var record))
            found = found || record.EntityId == world.GetPermanentId(entity);
        found.Should().BeTrue();
    }

    [Fact]
    public void MergeFinalCheckpoint_MergesEverythingWrittenSoFarIntoTheCheckpoint()
    {
        var world = new World();
        var registry = BuildRegistry();
        using var capture = new ChangeCapture(world, registry);
        var walStore = new FileWalStore(WalBasePath);
        var checkpointStore = new FileStore(CheckpointPath);
        var worker = new ContinuousWalWorker(world, capture, checkpointStore, walStore, WalOptions.Default);

        world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();
        world.AdvanceTick();
        worker.WalWriteCycle();
        worker.Dispose();

        worker.MergeFinalCheckpoint();

        var (_, entries, _, _) = CheckpointBuilder.ReadCheckpoint(checkpointStore);
        entries.Keys.Should().Contain(k => k.Discriminator == "Position");
    }

    [Fact]
    public void MergeFinalCheckpoint_RetiresEveryWalSegment()
    {
        var world = new World();
        var registry = BuildRegistry();
        using var capture = new ChangeCapture(world, registry);
        var walStore = new FileWalStore(WalBasePath);
        var checkpointStore = new FileStore(CheckpointPath);
        var worker = new ContinuousWalWorker(world, capture, checkpointStore, walStore, WalOptions.Default);

        world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();
        world.AdvanceTick();
        worker.WalWriteCycle();
        worker.Dispose();

        worker.MergeFinalCheckpoint();

        walStore.ListSegmentStartTicks().Should().BeEmpty();
    }
}
