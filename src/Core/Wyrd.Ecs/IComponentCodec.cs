namespace Wyrd.Ecs;

/// <summary>
/// A type-erased view over one component type's registration: its stable wire
/// discriminator, its current-process <see cref="Internal.TypeIndex{T}"/>, and the
/// ability to serialize a row out of a type-erased storage array or deserialize bytes
/// into a <see cref="World"/> via <see cref="CommandBuffer"/>. Obtained from
/// <see cref="CodecRegistry"/>.
/// </summary>
public interface IComponentCodec
{
    /// <summary>The stable wire discriminator this type was registered under.</summary>
    string Discriminator { get; }

    /// <summary>This type's current-process <see cref="Internal.TypeIndex{T}"/>: an in-memory optimization detail, never persisted.</summary>
    int TypeIndex { get; }

    /// <summary>
    /// A compile-time-derived hash of this type's field names and types, or <c>null</c>
    /// if this registration didn't supply one. <c>null</c> means no schema-mismatch
    /// check is ever performed for this type, on save or load.
    /// </summary>
    uint? SchemaHash { get; }

    /// <summary>Serializes the component at <paramref name="row"/> in <paramref name="rawItems"/> (a component storage's <c>RawItems</c> array, of this registration's concrete component type).</summary>
    byte[] EncodeRow(Array rawItems, int row);

    /// <summary>
    /// Encodes <paramref name="value"/>, a boxed value of this registration's concrete
    /// component type (as produced by the change-tracking scan feeding
    /// <see cref="World.Subscribe(IComponentCodec)"/>). Passing anything else throws
    /// <see cref="InvalidCastException"/>.
    /// </summary>
    byte[] EncodeValue(object value);

    /// <summary>
    /// Deserializes <paramref name="data"/> into a boxed value of this registration's
    /// concrete component type, for a caller that needs the value itself rather than to
    /// apply it to a <see cref="World"/> (see <see cref="DecodeInto"/> for that). The
    /// inverse of <see cref="EncodeValue"/>.
    /// </summary>
    object DecodeValue(byte[] data);

    /// <summary>
    /// Deserializes <paramref name="data"/> and queues adding it to <paramref name="entity"/>
    /// in <paramref name="world"/> as this registration's concrete component type, via
    /// <see cref="CommandBuffer.AddComponent{T}"/>: same silent-no-op-if-not-alive contract as
    /// every other structural mutation. Call <see cref="World.ApplyCommands()"/> to make it
    /// take effect. A caller that needs to detect corrupt source data (a reference to an
    /// entity that never landed) should check for that at the loading layer; this primitive
    /// doesn't throw for a missing entity.
    /// </summary>
    void DecodeInto(World world, Entity entity, byte[] data);
}
