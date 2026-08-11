namespace Wyrd.Ecs.Debug.Internal;

/// <summary>
/// Correlates a component's debug display name (what a snapshot's
/// <see cref="EncodedComponent.Discriminator"/> actually carries - resolved via
/// <see cref="Wyrd.Ecs.Internal.DebugNameRegistry"/>, not a real wire discriminator) back to
/// the <see cref="CodecRegistry"/> registration the edit path needs. A plain scan over
/// <see cref="CodecRegistry.All"/>, not a prebuilt index: both that name and a codec's
/// <see cref="IComponentCodec.TypeIndex"/> key off the same runtime
/// <see cref="Wyrd.Ecs.Internal.TypeIndex{T}"/> space, so the comparison is reliable even
/// when a type's real discriminator differs from its debug name - and this only ever runs
/// once per user-initiated edit, so a scan needs no caching to stay cheap.
/// </summary>
internal static class CodecRegistryDebugNameExtensions
{
    public static bool TryGetByDebugName(this CodecRegistry registry, string debugName, out IComponentCodec codec)
    {
        foreach (var candidate in registry.All)
        {
            if (Wyrd.Ecs.Internal.DebugNameRegistry.TryGetName(candidate.TypeIndex, out var name) && name == debugName)
            {
                codec = candidate;
                return true;
            }
        }

        codec = null!;
        return false;
    }
}
