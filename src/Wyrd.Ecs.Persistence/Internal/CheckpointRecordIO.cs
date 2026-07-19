using System.IO.Hashing;
using System.Text;

namespace Wyrd.Ecs.Persistence.Internal;

/// <summary>
/// Reads and writes one checkpoint record: a permanent <see cref="EntityId"/>, a
/// component's stable wire discriminator, and its serialized payload bytes, framed as
/// a length-prefixed block plus a CRC32 checksum. <see cref="TryReadRecord"/> returns
/// false (never throws) on any short read or checksum mismatch, so a file truncated or
/// corrupted mid-record by a crash mid-write is detected and replay cleanly stops at
/// the last complete, valid record instead of misreading garbage as data.
/// </summary>
internal static class CheckpointRecordIO
{
    public static void WriteRecord(Stream stream, EntityId entityId, string discriminator, byte[] payload)
    {
        using var recordBuffer = new MemoryStream();
        using (var writer = new BinaryWriter(recordBuffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ulong)(entityId.Value >> 64));
            writer.Write((ulong)entityId.Value);
            writer.Write(discriminator);
            writer.Write(payload.Length);
            writer.Write(payload);
        }

        var recordBytes = recordBuffer.ToArray();
        var checksum = Crc32.HashToUInt32(recordBytes);

        using var outWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        outWriter.Write(recordBytes.Length);
        outWriter.Write(recordBytes);
        outWriter.Write(checksum);
    }

    public static bool TryReadRecord(Stream stream, out EntityId entityId, out string discriminator, out byte[] payload)
    {
        entityId = default;
        discriminator = string.Empty;
        payload = [];

        Span<byte> lengthBuffer = stackalloc byte[4];
        if (!TryReadFully(stream, lengthBuffer)) return false;
        var recordLength = BitConverter.ToInt32(lengthBuffer);
        if (recordLength < 0) return false;

        var recordBytes = new byte[recordLength];
        if (!TryReadFully(stream, recordBytes)) return false;

        Span<byte> checksumBuffer = stackalloc byte[4];
        if (!TryReadFully(stream, checksumBuffer)) return false;
        var expectedChecksum = BitConverter.ToUInt32(checksumBuffer);

        if (Crc32.HashToUInt32(recordBytes) != expectedChecksum) return false;

        using var reader = new BinaryReader(new MemoryStream(recordBytes), Encoding.UTF8);
        var upper = reader.ReadUInt64();
        var lower = reader.ReadUInt64();
        entityId = new EntityId(new UInt128(upper, lower));
        discriminator = reader.ReadString();
        var payloadLength = reader.ReadInt32();
        payload = reader.ReadBytes(payloadLength);
        return true;
    }

    private static bool TryReadFully(Stream stream, Span<byte> buffer)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = stream.Read(buffer[totalRead..]);
            if (read == 0) return false;
            totalRead += read;
        }
        return true;
    }
}
