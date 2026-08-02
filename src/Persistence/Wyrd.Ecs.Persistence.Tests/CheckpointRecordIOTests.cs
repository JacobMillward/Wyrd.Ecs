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

        CheckpointRecordIO.TryReadRecord(stream, out _, out var readEntityId, out _, out var discriminator, out var schemaHash, out var payload).Should().BeTrue();

        readEntityId.Should().Be(entityId);
        discriminator.Should().Be("Position");
        schemaHash.Should().Be(42u);
        payload.Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void WriteRecord_WithNoSchemaHash_RoundTripsAsNull()
    {
        using var stream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(stream, EntityId.NewId(), "Position", null, [1]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out _, out _, out _, out _, out var schemaHash, out _).Should().BeTrue();

        schemaHash.Should().BeNull();
    }

    [Fact]
    public void WriteRecord_WithSchemaHashOfExactlyZero_RoundTripsAsZeroNotNull()
    {
        using var stream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(stream, EntityId.NewId(), "Position", 0u, [1]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out _, out _, out _, out _, out var schemaHash, out _).Should().BeTrue();

        schemaHash.Should().Be(0u);
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

        CheckpointRecordIO.TryReadRecord(stream, out _, out var readFirst, out _, out var firstDiscriminator, out var firstHash, out _).Should().BeTrue();
        readFirst.Should().Be(first);
        firstDiscriminator.Should().Be("Position");
        firstHash.Should().Be(1u);

        CheckpointRecordIO.TryReadRecord(stream, out _, out var readSecond, out _, out var secondDiscriminator, out var secondHash, out _).Should().BeTrue();
        readSecond.Should().Be(second);
        secondDiscriminator.Should().Be("Velocity");
        secondHash.Should().Be(2u);
    }

    [Fact]
    public void WriteRecord_RoundTripsAsComponentKind()
    {
        using var stream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(stream, EntityId.NewId(), "Position", null, [1]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out var kind, out _, out _, out _, out _, out _).Should().BeTrue();

        kind.Should().Be(CheckpointRecordKind.Component);
    }

    [Fact]
    public void WriteRelationRecord_ThenTryReadRecord_RoundTripsAllFields()
    {
        using var stream = new MemoryStream();
        var sourceId = EntityId.NewId();
        var targetId = EntityId.NewId();
        CheckpointRecordIO.WriteRelationRecord(stream, sourceId, targetId, "likes", 42u, [1, 2, 3]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out var kind, out var readSourceId, out var readTargetId, out var discriminator, out var schemaHash, out var payload).Should().BeTrue();

        kind.Should().Be(CheckpointRecordKind.RelationEdge);
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
        CheckpointRecordIO.WriteRecord(stream, componentEntity, "Position", null, [1]);
        CheckpointRecordIO.WriteRelationRecord(stream, sourceId, targetId, "likes", null, [2]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out var firstKind, out var firstEntityId, out _, out var firstDiscriminator, out _, out _).Should().BeTrue();
        firstKind.Should().Be(CheckpointRecordKind.Component);
        firstEntityId.Should().Be(componentEntity);
        firstDiscriminator.Should().Be("Position");

        CheckpointRecordIO.TryReadRecord(stream, out var secondKind, out var secondSourceId, out var secondTargetId, out var secondDiscriminator, out _, out _).Should().BeTrue();
        secondKind.Should().Be(CheckpointRecordKind.RelationEdge);
        secondSourceId.Should().Be(sourceId);
        secondTargetId.Should().Be(targetId);
        secondDiscriminator.Should().Be("likes");
    }

    [Fact]
    public void TryReadRecord_OnAnEmptyStream_ReturnsFalse()
    {
        using var stream = new MemoryStream();

        CheckpointRecordIO.TryReadRecord(stream, out _, out _, out _, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadRecord_OnAStreamTruncatedMidRecord_ReturnsFalseWithoutThrowing()
    {
        using var fullStream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(fullStream, EntityId.NewId(), "Position", null, [1, 2, 3, 4, 5]);
        var fullBytes = fullStream.ToArray();
        var truncatedBytes = fullBytes[..(fullBytes.Length - 3)];

        using var truncatedStream = new MemoryStream(truncatedBytes);
        var act = () => CheckpointRecordIO.TryReadRecord(truncatedStream, out _, out _, out _, out _, out _, out _);
        act.Should().NotThrow();

        using var truncatedStreamAgain = new MemoryStream(truncatedBytes);
        CheckpointRecordIO.TryReadRecord(truncatedStreamAgain, out _, out _, out _, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadRecord_OnARecordWithACorruptedByte_ReturnsFalse()
    {
        using var fullStream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(fullStream, EntityId.NewId(), "Position", null, [1, 2, 3]);
        var bytes = fullStream.ToArray();
        bytes[^6] ^= 0xFF; // flip a bit inside the record content, before the trailing checksum

        using var corruptedStream = new MemoryStream(bytes);

        CheckpointRecordIO.TryReadRecord(corruptedStream, out _, out _, out _, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadRecord_WithACorruptedLengthPrefixClaimingMoreDataThanTheStreamHas_ReturnsFalseWithoutThrowing()
    {
        using var fullStream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(fullStream, EntityId.NewId(), "Position", null, [1, 2, 3]);
        var bytes = fullStream.ToArray();
        BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 0); // corrupt the length prefix itself, not the body

        using var corruptedStream = new MemoryStream(bytes);
        var act = () => CheckpointRecordIO.TryReadRecord(corruptedStream, out _, out _, out _, out _, out _, out _);

        act.Should().NotThrow();
        corruptedStream.Position = 0;
        CheckpointRecordIO.TryReadRecord(corruptedStream, out _, out _, out _, out _, out _, out _).Should().BeFalse();
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
        CheckpointRecordIO.TryReadRecord(stream, out _, out var readEntityId, out _, out var discriminator, out var schemaHash, out var payload).Should().BeTrue();

        tick.Should().Be(7);
        readEntityId.Should().Be(entityId);
        discriminator.Should().Be("Position");
        schemaHash.Should().Be(7u);
        payload.Should().Equal(new byte[] { 9, 9 });
    }
}
