namespace Wyrd.Ecs;

/// <summary>
/// A type-erased view over one relation payload type's registration — mirrors
/// <see cref="IComponentCodec"/>, trimmed to what a relation edge needs: one payload
/// value at a time, never the owning <see cref="RelationLinks{T}"/> dictionary.
/// Obtained from <see cref="ComponentCodecRegistry"/>.
/// </summary>
public interface IRelationCodec
{
    /// <summary>The stable wire discriminator this relation type was registered under.</summary>
    string Discriminator { get; }

    /// <summary>This relation type's current-process <see cref="Internal.TypeIndex{T}"/> — an in-memory optimization detail, never persisted.</summary>
    int TypeIndex { get; }

    /// <summary>A compile-time-derived hash of this type's field names and types, or <c>null</c> if none was supplied.</summary>
    uint? SchemaHash { get; }

    /// <summary>Encodes <paramref name="value"/>, a boxed instance of this registration's concrete relation type.</summary>
    byte[] EncodeValue(object value);

    /// <summary>
    /// Deserializes <paramref name="data"/> and queues linking <paramref name="source"/>
    /// to <paramref name="target"/> via <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/>.
    /// Call <see cref="World.ApplyCommands()"/> to make it take effect.
    /// </summary>
    void DecodeInto(World world, Entity source, Entity target, byte[] data);
}
