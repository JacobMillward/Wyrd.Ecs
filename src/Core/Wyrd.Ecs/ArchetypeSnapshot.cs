namespace Wyrd.Ecs;

/// <summary>
/// One archetype's debug/inspection view: how many live entities it holds, and which
/// registered component/tag types are present on it. Returned by
/// <see cref="World.EnumerateArchetypes"/>. Read-once-and-display — not compared for
/// equality by any consumer this type has, so the default record-generated equality
/// (reference equality on the list fields) is left as-is rather than mirroring
/// <see cref="EncodedComponent"/>'s custom byte-content override.
/// </summary>
public readonly record struct ArchetypeSnapshot(
    int EntityCount,
    IReadOnlyList<string> ComponentDiscriminators,
    IReadOnlyList<string> TagDiscriminators);
