namespace Wyrd.Ecs;

/// <summary>
/// One component change, encoded: the entity, the tick it was touched on, the stable
/// wire discriminator of its component type, its registered schema hash (or
/// <c>null</c>), and its encoded bytes. Yielded by
/// <see cref="IComponentCodec.EncodeChanges"/>.
/// </summary>
public readonly record struct EncodedChange(Entity Entity, int Tick, string Discriminator, uint? SchemaHash, byte[] Data)
{
    /// <summary>
    /// Value equality over <see cref="Data"/>'s contents, not the default record
    /// struct behavior a <c>byte[]</c> field would otherwise get (reference equality).
    /// </summary>
    public bool Equals(EncodedChange other) =>
        Entity == other.Entity &&
        Tick == other.Tick &&
        Discriminator == other.Discriminator &&
        SchemaHash == other.SchemaHash &&
        Data.AsSpan().SequenceEqual(other.Data);

    /// <inheritdoc cref="Equals(EncodedChange)"/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Entity);
        hash.Add(Tick);
        hash.Add(Discriminator);
        hash.Add(SchemaHash);
        foreach (var b in Data) hash.Add(b);
        return hash.ToHashCode();
    }
}
