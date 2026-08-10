namespace Wyrd.Ecs.Debug.Internal;

internal sealed record WorldSnapshot(IReadOnlyList<ArchetypeSnapshot> Archetypes, IReadOnlyList<EntitySnapshot> Entities);
