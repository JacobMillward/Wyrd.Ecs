namespace Wyrd.Ecs;

/// <summary>
/// A type-erased view over one component type's registration — its stable wire
/// discriminator, its current-process <see cref="Internal.TypeIndex{T}"/>, and the
/// ability to serialize a row out of a type-erased storage array or deserialize bytes
/// directly into a <see cref="World"/>, without the caller needing to know the concrete
/// component type. Obtained from <see cref="SerializerRegistry"/>.
/// </summary>
public interface IRegisteredComponentType
{
    /// <summary>The stable wire discriminator this type was registered under.</summary>
    string Discriminator { get; }

    /// <summary>This type's current-process <see cref="Internal.TypeIndex{T}"/> — an in-memory optimization detail, never persisted.</summary>
    int TypeIndex { get; }

    /// <summary>Serializes the component at <paramref name="row"/> in <paramref name="rawItems"/> (a component storage's <c>RawItems</c> array, of this registration's concrete component type).</summary>
    byte[] SerializeRow(Array rawItems, int row);

    /// <summary>
    /// Deserializes <paramref name="data"/> and adds it to <paramref name="entity"/> in
    /// <paramref name="world"/> as this registration's concrete component type,
    /// immediately — not deferred through <see cref="Commands"/>. This is a controlled
    /// loading primitive (reconstructing a world from a save, or a future shard
    /// migration), not general gameplay mutation: there is nothing else running
    /// concurrently to corrupt in that context, and a not-alive <paramref name="entity"/>
    /// means the source data is corrupt, which should throw rather than silently no-op
    /// the way a queued <see cref="Commands"/> operation deliberately does. Like other
    /// low-level primitives in this engine, the caller is responsible for not calling
    /// this while a <see cref="IWorld"/> query is iterating the same archetype.
    /// </summary>
    void DeserializeInto(World world, Entity entity, byte[] data);
}
