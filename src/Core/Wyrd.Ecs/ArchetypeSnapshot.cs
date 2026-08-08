namespace Wyrd.Ecs;

/// <summary>
/// One archetype's debug/inspection view: how many live entities it holds, and which
/// registered component/tag types are present on it. Returned by both
/// <c>World.EnumerateArchetypes</c> overloads.
/// </summary>
public readonly record struct ArchetypeSnapshot(
    int EntityCount,
    IReadOnlyList<string> ComponentDiscriminators,
    IReadOnlyList<string> TagDiscriminators);
