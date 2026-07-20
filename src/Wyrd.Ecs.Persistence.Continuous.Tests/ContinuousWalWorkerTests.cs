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

    private static ComponentCodecRegistry BuildRegistry()
    {
        var registry = new ComponentCodecRegistry();
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

        var entity = world.Commands.CreateEntity(new Position { X = 5f });
        world.ApplyCommands();
        world.AdvanceTick();

        worker.WalWriteCycle();

        using var readStream = walStore.OpenSegmentRead(walStore.ListSegmentStartTicks()[0]);
        WalSegmentIO.ReadHeader(readStream);
        var records = new List<(WalRecordKind Kind, EntityId EntityId, string Discriminator)>();
        while (WalSegmentIO.TryReadRecord(readStream, out var kind, out _, out var readEntity, out var discriminator, out _, out _))
            records.Add((kind, readEntity, discriminator));

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
        WalSegmentIO.TryReadRecord(readStream, out _, out _, out _, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void WalWriteCycle_WhenFlushingTheWalStoreThrows_ReportsViaTheErrorCallbackAndDoesNotThrow()
    {
        var world = new World();
        using var capture = new ChangeCapture(world, BuildRegistry());
        // Segment opening must succeed (it happens inside the constructor, outside any
        // try/catch — a broken store should fail construction loudly, not be silently
        // swallowed) but Flush, called every WalWriteCycle, is what this test breaks.
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
}
