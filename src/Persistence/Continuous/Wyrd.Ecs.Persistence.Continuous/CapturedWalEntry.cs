namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// One captured change, ready to write through
/// <see cref="Internal.WalSegmentIO.WriteRecord(Stream, WalRecordKind, int, EntityId, string, uint?, byte[])"/>
/// with no further translation — the field order and types deliberately match that
/// method's parameters. Produced by the tick-driven capture step (component value
/// changes) and the structural observer (entity/component lifecycle events).
/// </summary>
public readonly record struct CapturedWalEntry(WalRecordKind Kind, int Tick, EntityId EntityId, string Discriminator, uint? SchemaHash, byte[] Payload)
{
    /// <summary>
    /// Value equality over <see cref="Payload"/>'s contents, not the default record
    /// struct behavior a <c>byte[]</c> field would otherwise get (reference equality) —
    /// the same reason <c>EncodedChange</c> and <c>EncodedComponent</c> override it too.
    /// </summary>
    public bool Equals(CapturedWalEntry other) =>
        Kind == other.Kind &&
        Tick == other.Tick &&
        EntityId == other.EntityId &&
        Discriminator == other.Discriminator &&
        SchemaHash == other.SchemaHash &&
        Payload.AsSpan().SequenceEqual(other.Payload);

    /// <inheritdoc cref="Equals(CapturedWalEntry)"/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(Tick);
        hash.Add(EntityId);
        hash.Add(Discriminator);
        hash.Add(SchemaHash);
        foreach (var b in Payload) hash.Add(b);
        return hash.ToHashCode();
    }
}
