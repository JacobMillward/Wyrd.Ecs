// TS-side equivalent of the C# ArchetypeKey type (Wyrd.Ecs's own components+tags
// archetype identity): a stable string, not the type itself, since the wire DTOs only
// ever carry the raw discriminator lists (see ArchetypeSnapshot/InspectedEntity).
// Sorting before joining means the two lists' original order never affects equality.
export function archetypeKey(identity: { componentDiscriminators: readonly string[]; tagDiscriminators: readonly string[] }): string {
    const components = [...identity.componentDiscriminators].sort().join(',');
    const tags = [...identity.tagDiscriminators].sort().join(',');
    return `${components}|${tags}`;
}
