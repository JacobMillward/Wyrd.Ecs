namespace Wyrd.Ecs.Internal;

/// <summary>
/// The concrete, generic-over-<typeparamref name="T"/> implementation behind
/// <see cref="IRegisteredComponentType"/> — the only place the downcast from a
/// type-erased <see cref="Array"/> back to <typeparamref name="T"/>[] happens, the same
/// pattern <see cref="ComponentStorage{T}.CopyRowTo"/> already uses for the same reason.
/// </summary>
internal sealed class RegisteredComponentType<T> : IRegisteredComponentType where T : struct, IComponent
{
    private readonly ComponentSerializer<T> _serialize;
    private readonly ComponentDeserializer<T> _deserialize;

    public string Discriminator { get; }
    public int TypeIndex { get; }

    internal RegisteredComponentType(string discriminator, ComponentSerializer<T> serialize, ComponentDeserializer<T> deserialize)
    {
        Discriminator = discriminator;
        TypeIndex = Internal.TypeIndex<T>.Value;
        _serialize = serialize;
        _deserialize = deserialize;
    }

    public byte[] SerializeRow(Array rawItems, int row) => _serialize(((T[])rawItems)[row]);

    public void DeserializeInto(World world, Entity entity, byte[] data) =>
        world.AddComponent<T>(entity) = _deserialize(data);
}
