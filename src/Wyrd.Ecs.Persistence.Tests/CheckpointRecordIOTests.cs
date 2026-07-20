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

        CheckpointRecordIO.TryReadRecord(stream, out var readEntityId, out var discriminator, out var schemaHash, out var payload).Should().BeTrue();

        readEntityId.Should().Be(entityId);
        discriminator.Should().Be("Position");
        schemaHash.Should().Be(42u);
        payload.Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void WriteRecord_WithNoSchemaHash_RoundTripsAsZero()
    {
        using var stream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(stream, EntityId.NewId(), "Position", 0u, [1]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out _, out _, out var schemaHash, out _).Should().BeTrue();

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

        CheckpointRecordIO.TryReadRecord(stream, out var readFirst, out var firstDiscriminator, out var firstHash, out _).Should().BeTrue();
        readFirst.Should().Be(first);
        firstDiscriminator.Should().Be("Position");
        firstHash.Should().Be(1u);

        CheckpointRecordIO.TryReadRecord(stream, out var readSecond, out var secondDiscriminator, out var secondHash, out _).Should().BeTrue();
        readSecond.Should().Be(second);
        secondDiscriminator.Should().Be("Velocity");
        secondHash.Should().Be(2u);
    }

    [Fact]
    public void TryReadRecord_OnAnEmptyStream_ReturnsFalse()
    {
        using var stream = new MemoryStream();

        CheckpointRecordIO.TryReadRecord(stream, out _, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadRecord_OnAStreamTruncatedMidRecord_ReturnsFalseWithoutThrowing()
    {
        using var fullStream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(fullStream, EntityId.NewId(), "Position", 0u, [1, 2, 3, 4, 5]);
        var fullBytes = fullStream.ToArray();
        var truncatedBytes = fullBytes[..(fullBytes.Length - 3)];

        using var truncatedStream = new MemoryStream(truncatedBytes);
        var act = () => CheckpointRecordIO.TryReadRecord(truncatedStream, out _, out _, out _, out _);
        act.Should().NotThrow();

        using var truncatedStreamAgain = new MemoryStream(truncatedBytes);
        CheckpointRecordIO.TryReadRecord(truncatedStreamAgain, out _, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadRecord_OnARecordWithACorruptedByte_ReturnsFalse()
    {
        using var fullStream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(fullStream, EntityId.NewId(), "Position", 0u, [1, 2, 3]);
        var bytes = fullStream.ToArray();
        bytes[^6] ^= 0xFF; // flip a bit inside the record content, before the trailing checksum

        using var corruptedStream = new MemoryStream(bytes);

        CheckpointRecordIO.TryReadRecord(corruptedStream, out _, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void WriteHeader_ThenReadHeader_Succeeds()
    {
        using var stream = new MemoryStream();
        CheckpointRecordIO.WriteHeader(stream);
        stream.Position = 0;

        var act = () => CheckpointRecordIO.ReadHeader(stream);

        act.Should().NotThrow();
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
    public void WriteHeader_ThenWriteRecord_ThenReadHeaderAndTryReadRecord_RoundTripsBoth()
    {
        using var stream = new MemoryStream();
        CheckpointRecordIO.WriteHeader(stream);
        var entityId = EntityId.NewId();
        CheckpointRecordIO.WriteRecord(stream, entityId, "Position", 7u, [9, 9]);
        stream.Position = 0;

        CheckpointRecordIO.ReadHeader(stream);
        CheckpointRecordIO.TryReadRecord(stream, out var readEntityId, out var discriminator, out var schemaHash, out var payload).Should().BeTrue();

        readEntityId.Should().Be(entityId);
        discriminator.Should().Be("Position");
        schemaHash.Should().Be(7u);
        payload.Should().Equal(new byte[] { 9, 9 });
    }
}
