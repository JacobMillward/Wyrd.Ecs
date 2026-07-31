namespace Wyrd.Ecs;

/// <summary>
/// One component, serialized: the entity it belongs to, the stable wire discriminator
/// of its component type (see <see cref="ComponentCodecRegistry"/>), its registered
/// schema hash (or <c>null</c> if none was supplied), and its serialized bytes. Yielded
/// by <see cref="World.EnumerateAll"/>.
/// </summary>
public readonly record struct EncodedComponent(Entity Entity, string Discriminator, uint? SchemaHash, byte[] Data)
{
    /// <summary>
    /// Value equality over <see cref="Data"/>'s contents, not the default record struct
    /// behavior a <c>byte[]</c> field would otherwise get (reference equality: arrays
    /// don't override <see cref="object.Equals(object)"/>, so two structurally identical
    /// payloads from separate encode calls would otherwise compare unequal).
    /// </summary>
    public bool Equals(EncodedComponent other) =>
        Entity == other.Entity &&
        Discriminator == other.Discriminator &&
        SchemaHash == other.SchemaHash &&
        Data.AsSpan().SequenceEqual(other.Data);

    /// <inheritdoc cref="Equals(EncodedComponent)"/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Entity);
        hash.Add(Discriminator);
        hash.Add(SchemaHash);
        foreach (var b in Data) hash.Add(b);
        return hash.ToHashCode();
    }
}
