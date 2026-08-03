namespace Wyrd.Ecs;

public sealed partial class World
{
    /// <summary>
    /// Every archetype with at least one live entity: its entity count and the
    /// registered component/tag discriminators present on it. A type index that
    /// resolves through neither <paramref name="registry"/>'s components nor its tags is
    /// silently skipped, same "unregistered things don't appear" contract
    /// <see cref="EnumerateAll"/> already documents. Debug/tooling path, not a per-tick
    /// one; to keep a call site out of a trimmed/Native AOT Release publish, wrap it in
    /// <c>#if DEBUG</c>/<c>[Conditional("DEBUG")]</c> in your own project. Eagerly
    /// materialized into a list, not lazily enumerated, since a lazily-suspended
    /// enumerator left open across a structural mutation would throw
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    public IReadOnlyList<ArchetypeSnapshot> EnumerateArchetypes(ComponentCodecRegistry registry)
    {
        var result = new List<ArchetypeSnapshot>();

        foreach (var archetype in _archetypes.Values)
        {
            if (archetype.Count == 0) continue;

            var components = new List<string>();
            foreach (var typeIndex in archetype.Signature.SetBits)
                if (registry.TryGetByTypeIndex(typeIndex, out var component))
                    components.Add(component.Discriminator);

            var tags = ResolveTagDiscriminators(archetype.Signature, registry);

            result.Add(new ArchetypeSnapshot(archetype.Count, components, tags));
        }

        return result;
    }

    /// <summary>
    /// Every tag discriminator set in <paramref name="signature"/> that
    /// <paramref name="registry"/> has a name for. Shared by
    /// <see cref="EnumerateArchetypes"/> and <see cref="EnumerateEntities"/>. Resolved
    /// once per archetype by each caller, not once per entity row, since the answer is
    /// identical for every entity in the same archetype.
    /// </summary>
    private static List<string> ResolveTagDiscriminators(Internal.TypeBitSet signature, ComponentCodecRegistry registry)
    {
        var tags = new List<string>();
        foreach (var typeIndex in signature.SetBits)
            if (registry.TryGetTagByTypeIndex(typeIndex, out var discriminator))
                tags.Add(discriminator);
        return tags;
    }

    /// <summary>
    /// Every live entity, including ones with no registered components or tags: the case
    /// <see cref="EnumerateAll"/> silently drops, since it only visits archetypes by
    /// walking their registered component storages. Debug/tooling path, not a per-tick
    /// one; same trimming guidance and eager-materialization reasoning as
    /// <see cref="EnumerateArchetypes"/> apply here too.
    /// </summary>
    public IReadOnlyList<EntitySnapshot> EnumerateEntities(ComponentCodecRegistry registry)
    {
        var result = new List<EntitySnapshot>();

        foreach (var archetype in _archetypes.Values)
        {
            if (archetype.Count == 0) continue;

            var tags = ResolveTagDiscriminators(archetype.Signature, registry);

            // Resolve which storages are registered once per archetype, not once per row:
            // the answer is identical for every entity in the archetype, so checking it
            // again for every row would just repeat the same dictionary lookups.
            var registeredStorages = new List<(Internal.IComponentStorage Storage, IComponentCodec Codec)>();
            foreach (var (typeIndex, storage) in archetype.Storages)
                if (registry.TryGetByTypeIndex(typeIndex, out var registered))
                    registeredStorages.Add((storage, registered));

            for (var row = 0; row < archetype.Count; row++)
            {
                var components = new List<EncodedComponent>(registeredStorages.Count);
                foreach (var (storage, registered) in registeredStorages)
                    components.Add(new EncodedComponent(archetype.Entities[row], registered.Discriminator, registered.SchemaHash, registered.EncodeRow(storage.RawItems, row)));

                result.Add(new EntitySnapshot(archetype.Entities[row], components, tags));
            }
        }

        return result;
    }
}
