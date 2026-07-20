namespace Wyrd.Ecs;

/// <summary>
/// One component, serialized: the entity it belongs to, the stable wire discriminator
/// of its component type (see <see cref="ComponentCodecRegistry"/>), its registered
/// schema hash (or <c>null</c> if none was supplied), and its serialized bytes. Yielded
/// by <see cref="IWorld.EnumerateAll"/>.
/// </summary>
public readonly record struct EncodedComponent(Entity Entity, string Discriminator, uint? SchemaHash, byte[] Data);
