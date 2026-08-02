namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// One captured change, ready to write via <see cref="Internal.WalSegmentIO"/> with no
/// further translation. Produced by the tick-driven capture step (component value
/// changes) and the structural observer (entity/component/relation lifecycle events).
/// </summary>
public readonly record struct CapturedWalEntry(WalRecordKind Kind, int Tick, EntityId EntityId, string Discriminator, uint? SchemaHash, byte[] Payload, EntityId? TargetId = null)
{
    /// <summary>
    /// Value equality over <see cref="Payload"/>'s contents, not the default record struct
    /// reference equality a <c>byte[]</c> field would otherwise get.
    /// </summary>
    public bool Equals(CapturedWalEntry other) =>
        Kind == other.Kind &&
        Tick == other.Tick &&
        EntityId == other.EntityId &&
        TargetId == other.TargetId &&
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
        hash.Add(TargetId);
        hash.Add(Discriminator);
        hash.Add(SchemaHash);
        foreach (var b in Payload) hash.Add(b);
        return hash.ToHashCode();
    }
}
