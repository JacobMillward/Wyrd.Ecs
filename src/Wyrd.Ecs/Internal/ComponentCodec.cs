namespace Wyrd.Ecs.Internal;

/// <summary>
/// The concrete, generic-over-<typeparamref name="T"/> implementation behind
/// <see cref="IComponentCodec"/> — the only place the downcast from a
/// type-erased <see cref="Array"/> back to <typeparamref name="T"/>[] happens, the same
/// pattern <see cref="ComponentStorage{T}.CopyRowTo"/> already uses for the same reason.
/// </summary>
internal sealed class ComponentCodec<T> : IComponentCodec where T : struct, IComponent
{
    private readonly ComponentEncoder<T> _encode;
    private readonly ComponentDecoder<T> _decode;

    public string Discriminator { get; }
    public int TypeIndex { get; }
    public uint? SchemaHash { get; }

    internal ComponentCodec(string discriminator, ComponentEncoder<T> encode, ComponentDecoder<T> decode, uint? schemaHash)
    {
        Discriminator = discriminator;
        TypeIndex = Internal.TypeIndex<T>.Value;
        SchemaHash = schemaHash;
        _encode = encode;
        _decode = decode;
    }

    public IDisposable EnableChangeTracking(World world) => world.TrackChanges<T>();

    public List<EncodedChange> EncodeChanges(World world, int sinceTick)
    {
        var changes = new List<EncodedChange>();
        foreach (var change in world.ReadChanges<T>(sinceTick))
            changes.Add(new EncodedChange(change.Entity, change.Tick, Discriminator, SchemaHash, _encode(change.Value)));
        return changes;
    }

    public byte[] EncodeRow(Array rawItems, int row) => _encode(((T[])rawItems)[row]);

    public void DecodeInto(World world, Entity entity, byte[] data) =>
        world.Commands.AddComponent(entity, _decode(data));
}
