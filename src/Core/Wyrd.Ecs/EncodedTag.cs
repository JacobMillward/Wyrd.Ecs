namespace Wyrd.Ecs;

/// <summary>No payload, no schema hash: a tag carries no data, so there's nothing beyond entity+discriminator to encode.</summary>
public readonly record struct EncodedTag(Entity Entity, string Discriminator);
