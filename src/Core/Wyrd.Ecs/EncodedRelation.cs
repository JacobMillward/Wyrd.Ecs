namespace Wyrd.Ecs;

/// <summary>
/// One relation edge, serialized: the source and target entity, the stable wire
/// discriminator of the relation payload type (see <see cref="ComponentCodecRegistry.RegisterRelation{T}"/>),
/// its registered schema hash (or <c>null</c> if none was supplied), and its serialized
/// payload bytes. Yielded by <see cref="World.EnumerateRelations"/>.
/// </summary>
public readonly record struct EncodedRelation(Entity Source, Entity Target, string Discriminator, uint? SchemaHash, byte[] Data)
{
    /// <summary>Value equality over <see cref="Data"/>'s contents. See <see cref="EncodedComponent.Equals(EncodedComponent)"/> for why.</summary>
    public bool Equals(EncodedRelation other) =>
        Source == other.Source &&
        Target == other.Target &&
        Discriminator == other.Discriminator &&
        SchemaHash == other.SchemaHash &&
        Data.AsSpan().SequenceEqual(other.Data);

    /// <inheritdoc cref="Equals(EncodedRelation)"/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Source);
        hash.Add(Target);
        hash.Add(Discriminator);
        hash.Add(SchemaHash);
        foreach (var b in Data) hash.Add(b);
        return hash.ToHashCode();
    }
}
