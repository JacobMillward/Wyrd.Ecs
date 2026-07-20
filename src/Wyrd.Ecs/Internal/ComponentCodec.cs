namespace Wyrd.Ecs.Internal;

/// <summary>
/// The concrete, generic-over-<typeparamref name="T"/> implementation behind
/// <see cref="IComponentCodec"/> — the only place the downcast from a
/// type-erased <see cref="Array"/> back to <typeparamref name="T"/>[] happens, the same
/// pattern <see cref="ComponentStorage{T}.CopyRowTo"/> already uses for the same reason.
/// </summary>
internal sealed class ComponentCodec<T> : IComponentCodec where T : struct, IComponent
{
    private readonly ComponentEncoder<T> _serialize;
    private readonly ComponentDecoder<T> _deserialize;

    public string Discriminator { get; }
    public int TypeIndex { get; }

    internal ComponentCodec(string discriminator, ComponentEncoder<T> serialize, ComponentDecoder<T> deserialize)
    {
        Discriminator = discriminator;
        TypeIndex = Internal.TypeIndex<T>.Value;
        _serialize = serialize;
        _deserialize = deserialize;
    }

    public byte[] EncodeRow(Array rawItems, int row) => _serialize(((T[])rawItems)[row]);

    public void DecodeInto(World world, Entity entity, byte[] data) =>
        world.Commands.AddComponent(entity, _deserialize(data));
}
