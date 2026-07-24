using Wyrd.Ecs.Persistence.Continuous.Internal;

namespace Wyrd.Ecs.Persistence.Continuous.Tests;

public class WalSegmentWriterTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"wyrd-continuous-walsegmentwriter-{Guid.NewGuid():N}");
    private string BasePath => Path.Combine(_directory, "world");

    public WalSegmentWriterTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void EnsureSegmentOpen_OnFirstCall_OpensASegmentAtTheGivenStartTick()
    {
        var walStore = new FileWalStore(BasePath);
        var writer = new WalSegmentWriter(walStore);

        writer.EnsureSegmentOpen(5);

        writer.CurrentSegmentStartTick.Should().Be(5);
        walStore.ListSegmentStartTicks().Should().Equal([5]);
    }

    [Fact]
    public void EnsureSegmentOpen_CalledAgainWithADifferentTick_IsANoOp()
    {
        var walStore = new FileWalStore(BasePath);
        var writer = new WalSegmentWriter(walStore);
        writer.EnsureSegmentOpen(5);

        writer.EnsureSegmentOpen(99);

        writer.CurrentSegmentStartTick.Should().Be(5);
        walStore.ListSegmentStartTicks().Should().Equal([5]);
    }

    [Fact]
    public void WriteRecords_WritesEveryReadyEntryToTheOpenSegment()
    {
        var walStore = new FileWalStore(BasePath);
        var writer = new WalSegmentWriter(walStore);
        writer.EnsureSegmentOpen(1);
        var entity = EntityId.NewId();
        var ready = new List<CapturedWalEntry> { new(WalRecordKind.ComponentChanged, 1, entity, "Position", 7u, [1, 2, 3]) };

        writer.WriteRecords(new DrainedChanges(ready, []));
        writer.Flush();

        using var readStream = walStore.OpenSegmentRead(1);
        WalSegmentIO.ReadHeader(readStream);
        WalSegmentIO.TryReadRecord(readStream, out var kind, out _, out var readEntity, out var discriminator, out _, out _).Should().BeTrue();
        kind.Should().Be(WalRecordKind.ComponentChanged);
        readEntity.Should().Be(entity);
        discriminator.Should().Be("Position");
    }

    [Fact]
    public void WriteRecords_EncodesPendingEntriesAtWriteTime()
    {
        var walStore = new FileWalStore(BasePath);
        var writer = new WalSegmentWriter(walStore);
        writer.EnsureSegmentOpen(1);
        var codec = new FakeCodec();
        var pending = new List<PendingValueChange> { new(codec, 1, EntityId.NewId(), 42f) };

        writer.WriteRecords(new DrainedChanges([], pending));
        writer.Flush();

        using var readStream = walStore.OpenSegmentRead(1);
        WalSegmentIO.ReadHeader(readStream);
        WalSegmentIO.TryReadRecord(readStream, out var kind, out _, out _, out var discriminator, out _, out var payload).Should().BeTrue();
        kind.Should().Be(WalRecordKind.ComponentChanged);
        discriminator.Should().Be("Fake");
        BitConverter.ToSingle(payload).Should().Be(42f);
    }

    [Fact]
    public void WriteRecords_MergesReadyAndPendingByTick_NotAllReadyThenAllPending()
    {
        var walStore = new FileWalStore(BasePath);
        var writer = new WalSegmentWriter(walStore);
        writer.EnsureSegmentOpen(1);
        var entity = EntityId.NewId();
        // An earlier-tick stale ComponentChanged (still pending, not yet encoded) and a
        // later-tick EntityDestroyed for the same entity, drained together in one
        // cycle. Written in tick order, EntityDestroyed must come last in the segment
        // so a checkpoint merge sees the destroy as the final word, not the stale value.
        var pending = new List<PendingValueChange> { new(new FakeCodec(), Tick: 1, entity, 1f) };
        var ready = new List<CapturedWalEntry> { new(WalRecordKind.EntityDestroyed, Tick: 2, entity, "", null, []) };

        writer.WriteRecords(new DrainedChanges(ready, pending));
        writer.Flush();

        using var readStream = walStore.OpenSegmentRead(1);
        WalSegmentIO.ReadHeader(readStream);
        var kinds = new List<WalRecordKind>();
        while (WalSegmentIO.TryReadRecord(readStream, out var kind, out _, out _, out _, out _, out _))
            kinds.Add(kind);

        kinds.Should().Equal(WalRecordKind.ComponentChanged, WalRecordKind.EntityDestroyed);
    }

    [Fact]
    public void WriteRecords_WithNoSegmentOpen_Throws()
    {
        var writer = new WalSegmentWriter(new FileWalStore(BasePath));

        var act = () => writer.WriteRecords(new DrainedChanges([], []));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rotate_ClosesTheOldSegmentAndOpensANewOne()
    {
        var walStore = new FileWalStore(BasePath);
        var writer = new WalSegmentWriter(walStore);
        writer.EnsureSegmentOpen(1);
        writer.WriteRecords(new DrainedChanges([new CapturedWalEntry(WalRecordKind.ComponentChanged, 1, EntityId.NewId(), "Position", null, [1])], []));

        writer.Rotate(newStartTick: 2);

        writer.CurrentSegmentStartTick.Should().Be(2);
        walStore.ListSegmentStartTicks().Should().Equal([1, 2]);
    }

    [Fact]
    public void Rotate_TheOldSegmentIsReadableAfterwards()
    {
        var walStore = new FileWalStore(BasePath);
        var writer = new WalSegmentWriter(walStore);
        writer.EnsureSegmentOpen(1);
        writer.WriteRecords(new DrainedChanges([new CapturedWalEntry(WalRecordKind.ComponentChanged, 1, EntityId.NewId(), "Position", null, [1])], []));
        writer.Flush();

        writer.Rotate(newStartTick: 2);

        using var readStream = walStore.OpenSegmentRead(1);
        WalSegmentIO.ReadHeader(readStream);
        WalSegmentIO.TryReadRecord(readStream, out _, out _, out _, out _, out _, out _).Should().BeTrue();
    }

    private sealed class FakeCodec : IComponentCodec
    {
        public string Discriminator => "Fake";
        public int TypeIndex => 0;
        public uint? SchemaHash => null;
        public IDisposable EnableChangeTracking(World world) => throw new NotSupportedException();
        public List<EncodedChange> EncodeChanges(World world, int sinceTick) => throw new NotSupportedException();
        public List<RawChange> ReadRawChanges(World world, int sinceTick) => throw new NotSupportedException();
        public byte[] EncodeRow(Array rawItems, int row) => throw new NotSupportedException();
        public byte[] EncodeValue(object value) => BitConverter.GetBytes((float)value);
        public void DecodeInto(World world, Entity entity, byte[] data) => throw new NotSupportedException();
    }
}
