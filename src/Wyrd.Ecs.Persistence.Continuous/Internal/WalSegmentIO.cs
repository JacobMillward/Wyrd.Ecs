using System.IO.Hashing;
using System.Text;

namespace Wyrd.Ecs.Persistence.Continuous.Internal;

/// <summary>
/// Reads and writes the WAL segment file header and individual WAL records. Mirrors
/// <c>Wyrd.Ecs.Persistence.Internal.CheckpointRecordIO</c>'s framing (magic bytes plus
/// version header, length-prefixed records with a CRC32 checksum, graceful
/// truncation-tolerant reads) — deliberately duplicated rather than shared across the
/// package boundary: <c>CheckpointRecordIO</c> is internal to
/// <c>Wyrd.Ecs.Persistence</c>, and a WAL record needs two fields a checkpoint record
/// doesn't — <see cref="WalRecordKind"/> and a tick. <see cref="TryReadRecord"/>
/// returns false (never throws) on any short read or checksum mismatch, so a segment
/// truncated or corrupted mid-record by a crash mid-write is detected and replay
/// cleanly stops at the last complete, valid record instead of misreading garbage as
/// data.
/// </summary>
internal static class WalSegmentIO
{
    private const uint MagicBytes = 0x314C4157;
    private const ushort FormatVersion = 1;

    public static void WriteHeader(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(MagicBytes);
        writer.Write(FormatVersion);
    }

    public static void ReadHeader(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        uint magic;
        try
        {
            magic = reader.ReadUInt32();
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("Not a valid Wyrd.Ecs WAL segment: the stream is too short to contain a header.");
        }

        if (magic != MagicBytes)
            throw new InvalidDataException("Not a valid Wyrd.Ecs WAL segment: bad magic bytes.");

        ushort version;
        try
        {
            version = reader.ReadUInt16();
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("Not a valid Wyrd.Ecs WAL segment: the stream is too short to contain a header.");
        }

        if (version != FormatVersion)
            throw new InvalidDataException($"Unsupported WAL segment format version {version} (expected {FormatVersion}).");
    }

    public static void WriteRecord(Stream stream, WalRecordKind kind, int tick, EntityId entityId, string discriminator, uint? schemaHash, byte[] payload)
    {
        using var recordBuffer = new MemoryStream();
        using (var writer = new BinaryWriter(recordBuffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)kind);
            writer.Write(tick);
            writer.Write((ulong)(entityId.Value >> 64));
            writer.Write((ulong)entityId.Value);
            writer.Write(discriminator);
            writer.Write(schemaHash.HasValue);
            writer.Write(schemaHash ?? 0);
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

    public static bool TryReadRecord(Stream stream, out WalRecordKind kind, out int tick, out EntityId entityId, out string discriminator, out uint? schemaHash, out byte[] payload)
    {
        kind = default;
        tick = 0;
        entityId = default;
        discriminator = string.Empty;
        schemaHash = null;
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
        kind = (WalRecordKind)reader.ReadByte();
        tick = reader.ReadInt32();
        var upper = reader.ReadUInt64();
        var lower = reader.ReadUInt64();
        entityId = new EntityId(new UInt128(upper, lower));
        discriminator = reader.ReadString();
        var hasSchemaHash = reader.ReadBoolean();
        var schemaHashValue = reader.ReadUInt32();
        schemaHash = hasSchemaHash ? schemaHashValue : null;
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
