using Wyrd.Ecs.Persistence.Continuous.Internal;

namespace Wyrd.Ecs.Persistence.Continuous.Tests;

public class WalSegmentIOTests
{
    [Fact]
    public void WriteRecord_ThenTryReadRecord_RoundTripsAllFields()
    {
        using var stream = new MemoryStream();
        var entityId = EntityId.NewId();
        WalSegmentIO.WriteRecord(stream, WalRecordKind.ComponentChanged, tick: 5, entityId, "Position", 42u, [1, 2, 3]);
        stream.Position = 0;

        WalSegmentIO.TryReadRecord(stream, out var kind, out var tick, out var readEntityId, out _, out var discriminator, out var schemaHash, out var payload).Should().BeTrue();

        kind.Should().Be(WalRecordKind.ComponentChanged);
        tick.Should().Be(5);
        readEntityId.Should().Be(entityId);
        discriminator.Should().Be("Position");
        schemaHash.Should().Be(42u);
        payload.Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void WriteRecord_WithNoSchemaHash_RoundTripsAsNull()
    {
        using var stream = new MemoryStream();
        WalSegmentIO.WriteRecord(stream, WalRecordKind.EntityCreated, tick: 1, EntityId.NewId(), "", null, []);
        stream.Position = 0;

        WalSegmentIO.TryReadRecord(stream, out _, out _, out _, out _, out _, out var schemaHash, out _).Should().BeTrue();

        schemaHash.Should().BeNull();
    }

    [Fact]
    public void WriteRecord_ForEveryRecordKind_RoundTripsTheKind()
    {
        foreach (var kind in Enum.GetValues<WalRecordKind>())
        {
            if (kind is WalRecordKind.RelationLinked or WalRecordKind.RelationUnlinked) continue; // written via WriteRelationRecord instead, covered by its own tests

            using var stream = new MemoryStream();
            WalSegmentIO.WriteRecord(stream, kind, tick: 1, EntityId.NewId(), "Position", null, []);
            stream.Position = 0;

            WalSegmentIO.TryReadRecord(stream, out var readKind, out _, out _, out _, out _, out _, out _).Should().BeTrue();

            readKind.Should().Be(kind);
        }
    }

    [Fact]
    public void WriteRecord_CalledMultipleTimes_EachRecordReadsBackInOrder()
    {
        using var stream = new MemoryStream();
        var first = EntityId.NewId();
        var second = EntityId.NewId();
        WalSegmentIO.WriteRecord(stream, WalRecordKind.ComponentChanged, tick: 1, first, "Position", 1u, [1]);
        WalSegmentIO.WriteRecord(stream, WalRecordKind.ComponentRemoved, tick: 2, second, "Velocity", null, []);
        stream.Position = 0;

        WalSegmentIO.TryReadRecord(stream, out var firstKind, out var firstTick, out var readFirst, out _, out var firstDiscriminator, out _, out _).Should().BeTrue();
        firstKind.Should().Be(WalRecordKind.ComponentChanged);
        firstTick.Should().Be(1);
        readFirst.Should().Be(first);
        firstDiscriminator.Should().Be("Position");

        WalSegmentIO.TryReadRecord(stream, out var secondKind, out var secondTick, out var readSecond, out _, out var secondDiscriminator, out _, out _).Should().BeTrue();
        secondKind.Should().Be(WalRecordKind.ComponentRemoved);
        secondTick.Should().Be(2);
        readSecond.Should().Be(second);
        secondDiscriminator.Should().Be("Velocity");
    }

    [Fact]
    public void WriteRelationRecord_ThenTryReadRecord_RoundTripsAllFields()
    {
        using var stream = new MemoryStream();
        var sourceId = EntityId.NewId();
        var targetId = EntityId.NewId();
        WalSegmentIO.WriteRelationRecord(stream, WalRecordKind.RelationLinked, tick: 3, sourceId, targetId, "likes", 42u, [1, 2, 3]);
        stream.Position = 0;

        WalSegmentIO.TryReadRecord(stream, out var kind, out var tick, out var readSourceId, out var readTargetId, out var discriminator, out var schemaHash, out var payload).Should().BeTrue();

        kind.Should().Be(WalRecordKind.RelationLinked);
        tick.Should().Be(3);
        readSourceId.Should().Be(sourceId);
        readTargetId.Should().Be(targetId);
        discriminator.Should().Be("likes");
        schemaHash.Should().Be(42u);
        payload.Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void WriteRecord_ThenWriteRelationRecord_EachReadsBackAsItsOwnKindInOrder()
    {
        using var stream = new MemoryStream();
        var componentEntity = EntityId.NewId();
        var sourceId = EntityId.NewId();
        var targetId = EntityId.NewId();
        WalSegmentIO.WriteRecord(stream, WalRecordKind.ComponentChanged, tick: 1, componentEntity, "Position", null, [1]);
        WalSegmentIO.WriteRelationRecord(stream, WalRecordKind.RelationUnlinked, tick: 2, sourceId, targetId, "likes", null, []);
        stream.Position = 0;

        WalSegmentIO.TryReadRecord(stream, out var firstKind, out _, out var firstEntityId, out _, out var firstDiscriminator, out _, out _).Should().BeTrue();
        firstKind.Should().Be(WalRecordKind.ComponentChanged);
        firstEntityId.Should().Be(componentEntity);
        firstDiscriminator.Should().Be("Position");

        WalSegmentIO.TryReadRecord(stream, out var secondKind, out _, out var secondSourceId, out var secondTargetId, out var secondDiscriminator, out _, out _).Should().BeTrue();
        secondKind.Should().Be(WalRecordKind.RelationUnlinked);
        secondSourceId.Should().Be(sourceId);
        secondTargetId.Should().Be(targetId);
        secondDiscriminator.Should().Be("likes");
    }

    [Fact]
    public void TryReadRecord_OnAnEmptyStream_ReturnsFalse()
    {
        using var stream = new MemoryStream();

        WalSegmentIO.TryReadRecord(stream, out _, out _, out _, out _, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadRecord_OnAStreamTruncatedMidRecord_ReturnsFalseWithoutThrowing()
    {
        using var fullStream = new MemoryStream();
        WalSegmentIO.WriteRecord(fullStream, WalRecordKind.ComponentChanged, tick: 1, EntityId.NewId(), "Position", null, [1, 2, 3, 4, 5]);
        var fullBytes = fullStream.ToArray();
        var truncatedBytes = fullBytes[..(fullBytes.Length - 3)];

        using var truncatedStream = new MemoryStream(truncatedBytes);
        var act = () => WalSegmentIO.TryReadRecord(truncatedStream, out _, out _, out _, out _, out _, out _, out _);
        act.Should().NotThrow();

        using var truncatedStreamAgain = new MemoryStream(truncatedBytes);
        WalSegmentIO.TryReadRecord(truncatedStreamAgain, out _, out _, out _, out _, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadRecord_OnARecordWithACorruptedByte_ReturnsFalse()
    {
        using var fullStream = new MemoryStream();
        WalSegmentIO.WriteRecord(fullStream, WalRecordKind.ComponentChanged, tick: 1, EntityId.NewId(), "Position", null, [1, 2, 3]);
        var bytes = fullStream.ToArray();
        bytes[^6] ^= 0xFF; // flip a bit inside the record content, before the trailing checksum

        using var corruptedStream = new MemoryStream(bytes);

        WalSegmentIO.TryReadRecord(corruptedStream, out _, out _, out _, out _, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadRecord_WithACorruptedLengthPrefixClaimingMoreDataThanTheStreamHas_ReturnsFalseWithoutThrowing()
    {
        using var fullStream = new MemoryStream();
        WalSegmentIO.WriteRecord(fullStream, WalRecordKind.ComponentChanged, tick: 1, EntityId.NewId(), "Position", null, [1, 2, 3]);
        var bytes = fullStream.ToArray();
        BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 0); // corrupt the length prefix itself, not the body

        using var corruptedStream = new MemoryStream(bytes);
        var act = () => WalSegmentIO.TryReadRecord(corruptedStream, out _, out _, out _, out _, out _, out _, out _);

        act.Should().NotThrow();
        corruptedStream.Position = 0;
        WalSegmentIO.TryReadRecord(corruptedStream, out _, out _, out _, out _, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void WriteHeader_ThenReadHeader_Succeeds()
    {
        using var stream = new MemoryStream();
        WalSegmentIO.WriteHeader(stream);
        stream.Position = 0;

        var act = () => WalSegmentIO.ReadHeader(stream);

        act.Should().NotThrow();
    }

    [Fact]
    public void ReadHeader_OnAStreamWithForeignMagicBytes_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream([0x00, 0x00, 0x00, 0x00, 0x01, 0x00]);

        var act = () => WalSegmentIO.ReadHeader(stream);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ReadHeader_OnAnEmptyStream_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();

        var act = () => WalSegmentIO.ReadHeader(stream);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void WriteHeader_ThenWriteRecord_ThenReadHeaderAndTryReadRecord_RoundTripsBoth()
    {
        using var stream = new MemoryStream();
        WalSegmentIO.WriteHeader(stream);
        var entityId = EntityId.NewId();
        WalSegmentIO.WriteRecord(stream, WalRecordKind.ComponentAdded, tick: 7, entityId, "Position", 3u, [9, 9]);
        stream.Position = 0;

        WalSegmentIO.ReadHeader(stream);
        WalSegmentIO.TryReadRecord(stream, out var kind, out var tick, out var readEntityId, out _, out var discriminator, out var schemaHash, out var payload).Should().BeTrue();

        kind.Should().Be(WalRecordKind.ComponentAdded);
        tick.Should().Be(7);
        readEntityId.Should().Be(entityId);
        discriminator.Should().Be("Position");
        schemaHash.Should().Be(3u);
        payload.Should().Equal(new byte[] { 9, 9 });
    }
}
