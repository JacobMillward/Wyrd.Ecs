using System.IO.Hashing;
using System.Text;

namespace Wyrd.Ecs.Persistence.Continuous.Internal;

/// <summary>
/// Reads and writes the WAL segment file header and individual WAL records: magic bytes
/// plus version header, length-prefixed records with a CRC32 checksum. A
/// <see cref="WalRecordKind.RelationLinked"/>/<see cref="WalRecordKind.RelationUnlinked"/>
/// record carries a second <see cref="EntityId"/> (the edge's target) right after the
/// first, written via <see cref="WriteRelationRecord(Stream, WalRecordKind, int, EntityId, EntityId, string, uint?, byte[])"/>
/// and read back via the same <see cref="TryReadRecord"/> every other kind uses.
/// <see cref="TryReadRecord"/> returns false, never throws, on any short read, checksum
/// mismatch, or corrupted length prefix, so replay stops cleanly at the last valid
/// record instead of misreading garbage left by a crash mid-write.
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

    /// <summary>Writes one record using a freshly allocated buffer, for a caller with no reusable buffer of its own. Use the other overload on a hot path, like <see cref="WalSegmentWriter"/> does, to avoid the per-call allocation.</summary>
    public static void WriteRecord(Stream stream, WalRecordKind kind, int tick, EntityId entityId, string discriminator, uint? schemaHash, byte[] payload)
    {
        using var recordBuffer = new MemoryStream();
        using var recordWriter = new BinaryWriter(recordBuffer, Encoding.UTF8, leaveOpen: true);
        WriteRecord(stream, recordBuffer, recordWriter, kind, tick, entityId, discriminator, schemaHash, payload);
    }

    /// <summary>
    /// Writes one record using <paramref name="recordBuffer"/>/<paramref name="recordWriter"/>
    /// as caller-owned, reused scratch space. Its contents are overwritten; don't rely on
    /// anything left in it after this returns.
    /// </summary>
    public static void WriteRecord(Stream stream, MemoryStream recordBuffer, BinaryWriter recordWriter, WalRecordKind kind, int tick, EntityId entityId, string discriminator, uint? schemaHash, byte[] payload)
    {
        recordBuffer.SetLength(0);
        recordWriter.Write((byte)kind);
        recordWriter.Write(tick);
        WriteEntityId(recordWriter, entityId);
        WriteTail(recordWriter, discriminator, schemaHash, payload);
        FlushRecord(stream, recordBuffer, recordWriter);
    }

    /// <summary>Same as <see cref="WriteRecord(Stream, WalRecordKind, int, EntityId, string, uint?, byte[])"/>, for a relation-edge record: carries a second <see cref="EntityId"/> for the edge's target.</summary>
    public static void WriteRelationRecord(Stream stream, WalRecordKind kind, int tick, EntityId sourceId, EntityId targetId, string discriminator, uint? schemaHash, byte[] payload)
    {
        using var recordBuffer = new MemoryStream();
        using var recordWriter = new BinaryWriter(recordBuffer, Encoding.UTF8, leaveOpen: true);
        WriteRelationRecord(stream, recordBuffer, recordWriter, kind, tick, sourceId, targetId, discriminator, schemaHash, payload);
    }

    /// <summary>Same as <see cref="WriteRecord(Stream, MemoryStream, BinaryWriter, WalRecordKind, int, EntityId, string, uint?, byte[])"/>, for a relation-edge record.</summary>
    public static void WriteRelationRecord(Stream stream, MemoryStream recordBuffer, BinaryWriter recordWriter, WalRecordKind kind, int tick, EntityId sourceId, EntityId targetId, string discriminator, uint? schemaHash, byte[] payload)
    {
        recordBuffer.SetLength(0);
        recordWriter.Write((byte)kind);
        recordWriter.Write(tick);
        WriteEntityId(recordWriter, sourceId);
        WriteEntityId(recordWriter, targetId);
        WriteTail(recordWriter, discriminator, schemaHash, payload);
        FlushRecord(stream, recordBuffer, recordWriter);
    }

    private static void WriteEntityId(BinaryWriter writer, EntityId entityId)
    {
        writer.Write((ulong)(entityId.Value >> 64));
        writer.Write((ulong)entityId.Value);
    }

    private static void WriteTail(BinaryWriter writer, string discriminator, uint? schemaHash, byte[] payload)
    {
        writer.Write(discriminator);
        writer.Write(schemaHash.HasValue);
        writer.Write(schemaHash ?? 0);
        writer.Write(payload.Length);
        writer.Write(payload);
    }

    private static void FlushRecord(Stream stream, MemoryStream recordBuffer, BinaryWriter recordWriter)
    {
        recordWriter.Flush();
        var recordBytes = recordBuffer.GetBuffer();
        var recordLength = (int)recordBuffer.Length;
        var checksum = Crc32.HashToUInt32(recordBytes.AsSpan(0, recordLength));

        using var outWriter = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        outWriter.Write(recordLength);
        outWriter.Write(recordBytes, 0, recordLength);
        outWriter.Write(checksum);
    }

    public static bool TryReadRecord(Stream stream, out WalRecordKind kind, out int tick, out EntityId entityId, out EntityId targetId, out string discriminator, out uint? schemaHash, out byte[] payload)
    {
        kind = default;
        tick = 0;
        entityId = default;
        targetId = default;
        discriminator = string.Empty;
        schemaHash = null;
        payload = [];

        Span<byte> lengthBuffer = stackalloc byte[4];
        if (!TryReadFully(stream, lengthBuffer)) return false;
        var recordLength = BitConverter.ToInt32(lengthBuffer);
        if (recordLength < 0) return false;
        if (stream.CanSeek && recordLength > stream.Length - stream.Position) return false;

        var recordBytes = new byte[recordLength];
        if (!TryReadFully(stream, recordBytes)) return false;

        Span<byte> checksumBuffer = stackalloc byte[4];
        if (!TryReadFully(stream, checksumBuffer)) return false;
        var expectedChecksum = BitConverter.ToUInt32(checksumBuffer);

        if (Crc32.HashToUInt32(recordBytes) != expectedChecksum) return false;

        using var reader = new BinaryReader(new MemoryStream(recordBytes), Encoding.UTF8);
        kind = (WalRecordKind)reader.ReadByte();
        tick = reader.ReadInt32();
        entityId = ReadEntityId(reader);
        if (kind is WalRecordKind.RelationLinked or WalRecordKind.RelationUnlinked)
            targetId = ReadEntityId(reader);

        discriminator = reader.ReadString();
        var hasSchemaHash = reader.ReadBoolean();
        var schemaHashValue = reader.ReadUInt32();
        schemaHash = hasSchemaHash ? schemaHashValue : null;
        var payloadLength = reader.ReadInt32();
        payload = reader.ReadBytes(payloadLength);
        return true;
    }

    private static EntityId ReadEntityId(BinaryReader reader)
    {
        var upper = reader.ReadUInt64();
        var lower = reader.ReadUInt64();
        return new EntityId(new UInt128(upper, lower));
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
