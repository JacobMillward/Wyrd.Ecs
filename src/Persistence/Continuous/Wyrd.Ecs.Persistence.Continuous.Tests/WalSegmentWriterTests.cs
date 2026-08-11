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
        WalSegmentIO.TryReadRecord(readStream, out var record).Should().BeTrue();
        record.Kind.Should().Be(WalRecordKind.ComponentChanged);
        record.EntityId.Should().Be(entity);
        record.Discriminator.Should().Be("Position");
    }

    [Fact]
    public void WriteRecords_RoutesARelationKindEntryThroughWriteRelationRecord()
    {
        var walStore = new FileWalStore(BasePath);
        var writer = new WalSegmentWriter(walStore);
        writer.EnsureSegmentOpen(1);
        var sourceId = EntityId.NewId();
        var targetId = EntityId.NewId();
        var ready = new List<CapturedWalEntry> { new(WalRecordKind.RelationLinked, 1, sourceId, "Likes", null, [1, 2, 3, 4], targetId) };

        writer.WriteRecords(new DrainedChanges(ready, []));
        writer.Flush();

        using var readStream = walStore.OpenSegmentRead(1);
        WalSegmentIO.ReadHeader(readStream);
        WalSegmentIO.TryReadRecord(readStream, out var record).Should().BeTrue();
        record.Kind.Should().Be(WalRecordKind.RelationLinked);
        record.EntityId.Should().Be(sourceId);
        record.TargetId.Should().Be(targetId);
        record.Discriminator.Should().Be("Likes");
        record.Payload.Should().Equal(new byte[] { 1, 2, 3, 4 });
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
        WalSegmentIO.TryReadRecord(readStream, out var record).Should().BeTrue();
        record.Kind.Should().Be(WalRecordKind.ComponentChanged);
        record.Discriminator.Should().Be("Fake");
        BitConverter.ToSingle(record.Payload).Should().Be(42f);
    }

    [Fact]
    public void WriteRecords_MergesReadyAndPendingByTick_NotAllReadyThenAllPending()
    {
        var walStore = new FileWalStore(BasePath);
        var writer = new WalSegmentWriter(walStore);
        writer.EnsureSegmentOpen(1);
        var entity = EntityId.NewId();
        // An earlier-tick stale ComponentChanged (still pending) and a later-tick
        // EntityDestroyed for the same entity, drained together. Written in tick order so
        // the destroy comes last and a checkpoint merge sees it as the final word.
        var pending = new List<PendingValueChange> { new(new FakeCodec(), Tick: 1, entity, 1f) };
        var ready = new List<CapturedWalEntry> { new(WalRecordKind.EntityDestroyed, Tick: 2, entity, "", null, []) };

        writer.WriteRecords(new DrainedChanges(ready, pending));
        writer.Flush();

        using var readStream = walStore.OpenSegmentRead(1);
        WalSegmentIO.ReadHeader(readStream);
        var kinds = new List<WalRecordKind>();
        while (WalSegmentIO.TryReadRecord(readStream, out var record))
            kinds.Add(record.Kind);

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
        WalSegmentIO.TryReadRecord(readStream, out _).Should().BeTrue();
    }

    [Fact]
    public void CloseCurrentSegment_ClosesWithoutOpeningAReplacement()
    {
        var walStore = new FileWalStore(BasePath);
        var writer = new WalSegmentWriter(walStore);
        writer.EnsureSegmentOpen(1);

        writer.CloseCurrentSegment();

        writer.CurrentSegmentStartTick.Should().Be(-1);
    }

    [Fact]
    public void CloseCurrentSegment_TheClosedSegmentIsReadableAfterwards()
    {
        var walStore = new FileWalStore(BasePath);
        var writer = new WalSegmentWriter(walStore);
        writer.EnsureSegmentOpen(1);
        writer.WriteRecords(new DrainedChanges([new CapturedWalEntry(WalRecordKind.ComponentChanged, 1, EntityId.NewId(), "Position", null, [1])], []));
        writer.Flush();

        writer.CloseCurrentSegment();

        using var readStream = walStore.OpenSegmentRead(1);
        WalSegmentIO.ReadHeader(readStream);
        WalSegmentIO.TryReadRecord(readStream, out _).Should().BeTrue();
    }

    private sealed class FakeCodec : IComponentCodec
    {
        public string Discriminator => "Fake";
        public int TypeIndex => 0;
        public uint? SchemaHash => null;
        public byte[] EncodeRow(Array rawItems, int row) => throw new NotSupportedException();
        public byte[] EncodeValue(object value) => BitConverter.GetBytes((float)value);
        public object DecodeValue(byte[] data) => BitConverter.ToSingle(data);
        public void DecodeInto(World world, Entity entity, byte[] data) => throw new NotSupportedException();
    }
}
