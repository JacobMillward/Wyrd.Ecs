using Wyrd.Ecs.Persistence.Internal;

namespace Wyrd.Ecs.Persistence.Tests;

public class CheckpointRecordIOTests
{
    [Fact]
    public void WriteRecord_ThenTryReadRecord_RoundTripsAllFields()
    {
        using var stream = new MemoryStream();
        var entityId = EntityId.NewId();
        CheckpointRecordIO.WriteRecord(stream, entityId, "Position", [1, 2, 3]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out var readEntityId, out var discriminator, out var payload).Should().BeTrue();

        readEntityId.Should().Be(entityId);
        discriminator.Should().Be("Position");
        payload.Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void WriteRecord_CalledMultipleTimes_EachRecordReadsBackInOrder()
    {
        using var stream = new MemoryStream();
        var first = EntityId.NewId();
        var second = EntityId.NewId();
        CheckpointRecordIO.WriteRecord(stream, first, "Position", [1]);
        CheckpointRecordIO.WriteRecord(stream, second, "Velocity", [2, 2]);
        stream.Position = 0;

        CheckpointRecordIO.TryReadRecord(stream, out var readFirst, out var firstDiscriminator, out _).Should().BeTrue();
        readFirst.Should().Be(first);
        firstDiscriminator.Should().Be("Position");

        CheckpointRecordIO.TryReadRecord(stream, out var readSecond, out var secondDiscriminator, out _).Should().BeTrue();
        readSecond.Should().Be(second);
        secondDiscriminator.Should().Be("Velocity");
    }

    [Fact]
    public void TryReadRecord_OnAnEmptyStream_ReturnsFalse()
    {
        using var stream = new MemoryStream();

        CheckpointRecordIO.TryReadRecord(stream, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadRecord_OnAStreamTruncatedMidRecord_ReturnsFalseWithoutThrowing()
    {
        using var fullStream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(fullStream, EntityId.NewId(), "Position", [1, 2, 3, 4, 5]);
        var fullBytes = fullStream.ToArray();
        var truncatedBytes = fullBytes[..(fullBytes.Length - 3)];

        using var truncatedStream = new MemoryStream(truncatedBytes);
        var act = () => CheckpointRecordIO.TryReadRecord(truncatedStream, out _, out _, out _);
        act.Should().NotThrow();

        using var truncatedStreamAgain = new MemoryStream(truncatedBytes);
        CheckpointRecordIO.TryReadRecord(truncatedStreamAgain, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryReadRecord_OnARecordWithACorruptedByte_ReturnsFalse()
    {
        using var fullStream = new MemoryStream();
        CheckpointRecordIO.WriteRecord(fullStream, EntityId.NewId(), "Position", [1, 2, 3]);
        var bytes = fullStream.ToArray();
        bytes[^6] ^= 0xFF; // flip a bit inside the record content, before the trailing checksum

        using var corruptedStream = new MemoryStream(bytes);

        CheckpointRecordIO.TryReadRecord(corruptedStream, out _, out _, out _).Should().BeFalse();
    }
}
