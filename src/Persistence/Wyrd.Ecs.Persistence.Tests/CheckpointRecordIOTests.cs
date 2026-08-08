using Wyrd.Ecs.Persistence.Internal;

namespace Wyrd.Ecs.Persistence.Tests;

public class CheckpointRecordIOTests
{
    [Fact]
    public void WriteRecord_ThenTryReadRecord_RoundTripsAllFields()
    {
        using var stream = new MemoryStream();
        var entityId = EntityId.NewId();
        CheckpointRecordIO.WriteRecord(stream, entityId, "Position", 42u, [1, 2, 3]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out var record).Should().BeTrue();

        record.EntityId.Should().Be(entityId);
        record.Discriminator.Should().Be("Position");
        record.SchemaHash.Should().Be(42u);
        record.Payload.Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void WriteRecord_WithNoSchemaHash_RoundTripsAsNull()
    {
        using var stream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(stream, EntityId.NewId(), "Position", null, [1]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out var record).Should().BeTrue();

        record.SchemaHash.Should().BeNull();
    }

    [Fact]
    public void WriteRecord_WithSchemaHashOfExactlyZero_RoundTripsAsZeroNotNull()
    {
        using var stream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(stream, EntityId.NewId(), "Position", 0u, [1]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out var record).Should().BeTrue();

        record.SchemaHash.Should().Be(0u);
    }

    [Fact]
    public void WriteRecord_CalledMultipleTimes_EachRecordReadsBackInOrder()
    {
        using var stream = new MemoryStream();
        var first = EntityId.NewId();
        var second = EntityId.NewId();
        CheckpointRecordIO.WriteRecord(stream, first, "Position", 1u, [1]);
        CheckpointRecordIO.WriteRecord(stream, second, "Velocity", 2u, [2, 2]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out var firstRecord).Should().BeTrue();
        firstRecord.EntityId.Should().Be(first);
        firstRecord.Discriminator.Should().Be("Position");
        firstRecord.SchemaHash.Should().Be(1u);

        CheckpointRecordIO.TryReadRecord(stream, out var secondRecord).Should().BeTrue();
        secondRecord.EntityId.Should().Be(second);
        secondRecord.Discriminator.Should().Be("Velocity");
        secondRecord.SchemaHash.Should().Be(2u);
    }

    [Fact]
    public void WriteRecord_RoundTripsAsComponentKind()
    {
        using var stream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(stream, EntityId.NewId(), "Position", null, [1]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out var record).Should().BeTrue();

        record.Kind.Should().Be(CheckpointRecordKind.Component);
    }

    [Fact]
    public void WriteRelationRecord_ThenTryReadRecord_RoundTripsAllFields()
    {
        using var stream = new MemoryStream();
        var sourceId = EntityId.NewId();
        var targetId = EntityId.NewId();
        CheckpointRecordIO.WriteRelationRecord(stream, sourceId, targetId, "likes", 42u, [1, 2, 3]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out var record).Should().BeTrue();

        record.Kind.Should().Be(CheckpointRecordKind.RelationEdge);
        record.EntityId.Should().Be(sourceId);
        record.TargetId.Should().Be(targetId);
        record.Discriminator.Should().Be("likes");
        record.SchemaHash.Should().Be(42u);
        record.Payload.Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void WriteRecord_ThenWriteRelationRecord_EachReadsBackAsItsOwnKindInOrder()
    {
        using var stream = new MemoryStream();
        var componentEntity = EntityId.NewId();
        var sourceId = EntityId.NewId();
        var targetId = EntityId.NewId();
        CheckpointRecordIO.WriteRecord(stream, componentEntity, "Position", null, [1]);
        CheckpointRecordIO.WriteRelationRecord(stream, sourceId, targetId, "likes", null, [2]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out var firstRecord).Should().BeTrue();
        firstRecord.Kind.Should().Be(CheckpointRecordKind.Component);
        firstRecord.EntityId.Should().Be(componentEntity);
        firstRecord.Discriminator.Should().Be("Position");

        CheckpointRecordIO.TryReadRecord(stream, out var secondRecord).Should().BeTrue();
        secondRecord.Kind.Should().Be(CheckpointRecordKind.RelationEdge);
        secondRecord.EntityId.Should().Be(sourceId);
        secondRecord.TargetId.Should().Be(targetId);
        secondRecord.Discriminator.Should().Be("likes");
    }

    [Fact]
    public void TryReadRecord_OnAnEmptyStream_ReturnsFalse()
    {
        using var stream = new MemoryStream();

        CheckpointRecordIO.TryReadRecord(stream, out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadRecord_OnAStreamTruncatedMidRecord_ReturnsFalseWithoutThrowing()
    {
        using var fullStream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(fullStream, EntityId.NewId(), "Position", null, [1, 2, 3, 4, 5]);
        var fullBytes = fullStream.ToArray();
        var truncatedBytes = fullBytes[..(fullBytes.Length - 3)];

        using var truncatedStream = new MemoryStream(truncatedBytes);
        var act = () => CheckpointRecordIO.TryReadRecord(truncatedStream, out _);
        act.Should().NotThrow();

        using var truncatedStreamAgain = new MemoryStream(truncatedBytes);
        CheckpointRecordIO.TryReadRecord(truncatedStreamAgain, out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadRecord_OnARecordWithACorruptedByte_ReturnsFalse()
    {
        using var fullStream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(fullStream, EntityId.NewId(), "Position", null, [1, 2, 3]);
        var bytes = fullStream.ToArray();
        bytes[^6] ^= 0xFF; // flip a bit inside the record content, before the trailing checksum

        using var corruptedStream = new MemoryStream(bytes);

        CheckpointRecordIO.TryReadRecord(corruptedStream, out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadRecord_WithACorruptedLengthPrefixClaimingMoreDataThanTheStreamHas_ReturnsFalseWithoutThrowing()
    {
        using var fullStream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(fullStream, EntityId.NewId(), "Position", null, [1, 2, 3]);
        var bytes = fullStream.ToArray();
        BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 0); // corrupt the length prefix itself, not the body

        using var corruptedStream = new MemoryStream(bytes);
        var act = () => CheckpointRecordIO.TryReadRecord(corruptedStream, out _);

        act.Should().NotThrow();
        corruptedStream.Position = 0;
        CheckpointRecordIO.TryReadRecord(corruptedStream, out _).Should().BeFalse();
    }

    [Fact]
    public void WriteHeader_ThenReadHeader_RoundTripsTheTick()
    {
        using var stream = new MemoryStream();
        CheckpointRecordIO.WriteHeader(stream, tick: 42);
        stream.Position = 0;

        var tick = CheckpointRecordIO.ReadHeader(stream);

        tick.Should().Be(42);
    }

    [Fact]
    public void ReadHeader_OnAStreamWithForeignMagicBytes_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream([0x00, 0x00, 0x00, 0x00, 0x01, 0x00]);

        var act = () => CheckpointRecordIO.ReadHeader(stream);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ReadHeader_OnAnEmptyStream_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream();

        var act = () => CheckpointRecordIO.ReadHeader(stream);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ReadHeader_OnAStreamWithValidMagicButNoVersion_ThrowsInvalidDataException()
    {
        using var stream = new MemoryStream([0x43, 0x45, 0x59, 0x57]);

        var act = () => CheckpointRecordIO.ReadHeader(stream);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void ReadHeader_OnAStreamWithValidMagicAndVersionButNoTick_ThrowsInvalidDataException()
    {
        // Magic bytes plus a 2-byte version and nothing else - enough for the first
        // two reads to succeed, not enough for the tick read that follows them.
        using var stream = new MemoryStream([0x43, 0x45, 0x59, 0x57, 0x03, 0x00]);

        var act = () => CheckpointRecordIO.ReadHeader(stream);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void WriteHeader_ThenWriteRecord_ThenReadHeaderAndTryReadRecord_RoundTripsBoth()
    {
        using var stream = new MemoryStream();
        CheckpointRecordIO.WriteHeader(stream, tick: 7);
        var entityId = EntityId.NewId();
        CheckpointRecordIO.WriteRecord(stream, entityId, "Position", 7u, [9, 9]);
        stream.Position = 0;

        var tick = CheckpointRecordIO.ReadHeader(stream);
        CheckpointRecordIO.TryReadRecord(stream, out var record).Should().BeTrue();

        tick.Should().Be(7);
        record.EntityId.Should().Be(entityId);
        record.Discriminator.Should().Be("Position");
        record.SchemaHash.Should().Be(7u);
        record.Payload.Should().Equal(new byte[] { 9, 9 });
    }
}
