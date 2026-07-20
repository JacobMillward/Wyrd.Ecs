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
    public void WriteRecords_WritesEveryEntryToTheOpenSegment()
    {
        var walStore = new FileWalStore(BasePath);
        var writer = new WalSegmentWriter(walStore);
        writer.EnsureSegmentOpen(1);
        var entity = EntityId.NewId();
        var entries = new List<CapturedWalEntry> { new(WalRecordKind.ComponentChanged, 1, entity, "Position", 7u, [1, 2, 3]) };

        writer.WriteRecords(entries);
        writer.Flush();

        using var readStream = walStore.OpenSegmentRead(1);
        WalSegmentIO.ReadHeader(readStream);
        WalSegmentIO.TryReadRecord(readStream, out var kind, out _, out var readEntity, out var discriminator, out _, out _).Should().BeTrue();
        kind.Should().Be(WalRecordKind.ComponentChanged);
        readEntity.Should().Be(entity);
        discriminator.Should().Be("Position");
    }

    [Fact]
    public void WriteRecords_WithNoSegmentOpen_Throws()
    {
        var writer = new WalSegmentWriter(new FileWalStore(BasePath));

        var act = () => writer.WriteRecords([]);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Rotate_ClosesTheOldSegmentAndOpensANewOne()
    {
        var walStore = new FileWalStore(BasePath);
        var writer = new WalSegmentWriter(walStore);
        writer.EnsureSegmentOpen(1);
        writer.WriteRecords([new CapturedWalEntry(WalRecordKind.ComponentChanged, 1, EntityId.NewId(), "Position", null, [1])]);

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
        writer.WriteRecords([new CapturedWalEntry(WalRecordKind.ComponentChanged, 1, EntityId.NewId(), "Position", null, [1])]);
        writer.Flush();

        writer.Rotate(newStartTick: 2);

        using var readStream = walStore.OpenSegmentRead(1);
        WalSegmentIO.ReadHeader(readStream);
        WalSegmentIO.TryReadRecord(readStream, out _, out _, out _, out _, out _, out _).Should().BeTrue();
    }
}
