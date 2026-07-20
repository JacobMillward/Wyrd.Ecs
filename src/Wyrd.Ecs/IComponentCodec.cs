namespace Wyrd.Ecs;

/// <summary>
/// A type-erased view over one component type's registration — its stable wire
/// discriminator, its current-process <see cref="Internal.TypeIndex{T}"/>, and the
/// ability to serialize a row out of a type-erased storage array or deserialize bytes
/// into a <see cref="World"/> via <see cref="CommandBuffer"/>, without the caller needing to
/// know the concrete component type. Obtained from <see cref="ComponentCodecRegistry"/>.
/// </summary>
public interface IComponentCodec
{
    /// <summary>The stable wire discriminator this type was registered under.</summary>
    string Discriminator { get; }

    /// <summary>This type's current-process <see cref="Internal.TypeIndex{T}"/> — an in-memory optimization detail, never persisted.</summary>
    int TypeIndex { get; }

    /// <summary>
    /// A compile-time-derived hash of this type's field names and types, or <c>null</c>
    /// if this registration didn't supply one. <c>null</c> means no schema-mismatch
    /// check is ever performed for this type, on save or load.
    /// </summary>
    uint? SchemaHash { get; }

    /// <summary>
    /// Turns change tracking on for this registration's concrete component type via
    /// <see cref="World.TrackChanges{T}"/>, without the caller needing to know that
    /// type. Dispose the returned handle to turn tracking back off, same contract as
    /// <see cref="World.TrackChanges{T}"/> itself.
    /// </summary>
    IDisposable EnableChangeTracking(World world);

    /// <summary>
    /// Scans for every change to this registration's concrete component type since
    /// <paramref name="sinceTick"/> via <see cref="World.ReadChanges{T}"/>, encoding
    /// each one — type-erased, the same way <see cref="EncodeRow"/> is. Only observes
    /// anything once <see cref="EnableChangeTracking"/> has been called for this type.
    /// Eagerly materialized into a <see cref="List{T}"/>, not lazily yielded: the
    /// underlying scan is a <c>ref struct</c> enumerator that cannot survive across a
    /// <c>yield return</c> boundary, and the intended caller (a background-persistence
    /// capture step) needs a fully-drained, plain buffer to hand off from the
    /// synchronous scanning thread anyway.
    /// </summary>
    List<EncodedChange> EncodeChanges(World world, int sinceTick);

    /// <summary>Serializes the component at <paramref name="row"/> in <paramref name="rawItems"/> (a component storage's <c>RawItems</c> array, of this registration's concrete component type).</summary>
    byte[] EncodeRow(Array rawItems, int row);

    /// <summary>
    /// Deserializes <paramref name="data"/> and queues adding it to <paramref name="entity"/>
    /// in <paramref name="world"/> as this registration's concrete component type, via
    /// <see cref="CommandBuffer.AddComponent{T}"/> — the exact same mechanism and the same
    /// silent-no-op-if-not-alive contract as every other structural mutation. This is
    /// deliberate, not just consistency for its own sake: deserializing writes a
    /// component and can move an entity between archetypes, the same operation
    /// <see cref="CommandBuffer.AddComponent{T}"/> already performs, and that operation is
    /// unsafe at the wrong time regardless of where the value came from — a network
    /// client applying received entity state, or a redundancy replica applying a stream
    /// to its own standby <see cref="World"/>, can do this while other things are
    /// running, not only during an isolated startup load. Call
    /// <see cref="World.ApplyCommands()"/> to make it take effect. A caller that needs to
    /// detect corrupt source data (a reference to an entity that never landed) should
    /// check for that at the loading layer, which has the context to know what "missing"
    /// means — not by expecting this primitive to throw.
    /// </summary>
    void DecodeInto(World world, Entity entity, byte[] data);
}
