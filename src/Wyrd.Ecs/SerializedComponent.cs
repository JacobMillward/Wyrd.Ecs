namespace Wyrd.Ecs;

/// <summary>
/// One component, serialized: the entity it belongs to, the stable wire discriminator
/// of its component type (see <see cref="SerializerRegistry"/>), and its serialized
/// bytes. Yielded by <see cref="IWorld.EnumerateAll"/>.
/// </summary>
public readonly record struct SerializedComponent(Entity Entity, string Discriminator, byte[] Data);
