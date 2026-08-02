namespace Wyrd.Ecs;

/// <summary>
/// A type-erased view over one relation payload type's registration. Mirrors
/// <see cref="IComponentCodec"/>, trimmed to what a relation edge needs: one payload
/// value at a time, never the owning <see cref="RelationLinks{T}"/> dictionary.
/// Obtained from <see cref="ComponentCodecRegistry"/>.
/// </summary>
public interface IRelationCodec
{
    /// <summary>The stable wire discriminator this relation type was registered under.</summary>
    string Discriminator { get; }

    /// <summary>This relation type's current-process <see cref="Internal.TypeIndex{T}"/>: an in-memory optimization detail, never persisted.</summary>
    int TypeIndex { get; }

    /// <summary>A compile-time-derived hash of this type's field names and types, or <c>null</c> if none was supplied.</summary>
    uint? SchemaHash { get; }

    /// <summary>Encodes <paramref name="value"/>, a boxed instance of this registration's concrete relation type.</summary>
    byte[] EncodeValue(object value);

    /// <summary>
    /// Reads and encodes the current payload of the edge from <paramref name="source"/>
    /// to <paramref name="target"/>. Only safe to call while that edge is known to
    /// still exist: <see cref="World.Targets{T}"/> throws if <paramref name="source"/>
    /// has no <c>RelationLinks{T}</c> component, or if <paramref name="target"/> isn't
    /// one of its keys.
    /// </summary>
    byte[] EncodeEdge(World world, Entity source, Entity target);

    /// <summary>
    /// Encodes every edge in <paramref name="rawItems"/>[<paramref name="row"/>] (a
    /// <c>RelationLinks{T}[]</c> component storage's <c>RawItems</c> array): one
    /// <c>(Target, Payload)</c> pair per target the source entity at that row has an
    /// edge to. Unlike a component's single-value row, one row here can yield any
    /// number of results, since one <c>RelationLinks{T}</c> value holds every edge for
    /// its owning entity.
    /// </summary>
    IEnumerable<(Entity Target, byte[] Payload)> EncodeRow(Array rawItems, int row);

    /// <summary>
    /// Deserializes <paramref name="data"/> and queues linking <paramref name="source"/>
    /// to <paramref name="target"/> via <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/>.
    /// Call <see cref="World.ApplyCommands()"/> to make it take effect.
    /// </summary>
    void DecodeInto(World world, Entity source, Entity target, byte[] data);
}
