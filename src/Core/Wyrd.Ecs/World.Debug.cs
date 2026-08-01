namespace Wyrd.Ecs;

public sealed partial class World
{
    /// <summary>
    /// Every archetype with at least one live entity: its entity count and the
    /// registered component/tag discriminators present on it. A type index that
    /// resolves through neither <paramref name="registry"/>'s components nor its tags is
    /// silently skipped, same "unregistered things don't appear" contract
    /// <see cref="EnumerateAll"/> already documents. Debug/tooling path — a full-world
    /// walk, not a per-tick one. To keep a call site out of a trimmed/Native AOT Release
    /// publish entirely, wrap the call itself in <c>#if DEBUG</c>/<c>[Conditional("DEBUG")]</c>
    /// in your own project — the trimmer removes this method transitively once nothing
    /// references it.
    ///
    /// <para>
    /// Eagerly materialized into a list, not lazily <c>yield return</c>-ed like
    /// <see cref="EnumerateAll"/> — <c>_archetypes</c> is a plain <c>Dictionary</c>, and a
    /// structural mutation reaching a never-before-seen archetype adds a new key to it.
    /// A lazily-suspended enumerator left open across such a mutation (exactly the
    /// "enumerate, then mutate one of the results" pattern a debug consumer will use)
    /// would throw <see cref="InvalidOperationException"/>. Buffering costs nothing this
    /// debug/tooling path cares about, so it's not a trade-off worth making.
    /// </para>
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
    /// <paramref name="registry"/> has a name for — shared by
    /// <see cref="EnumerateArchetypes"/> and <see cref="EnumerateEntities"/>, since both
    /// need the exact same per-archetype tag resolution. Resolved once per archetype by
    /// each caller, not once per entity row — the answer is identical for every entity in
    /// the same archetype, so re-resolving per row would just be repeated dictionary
    /// lookups for no reason.
    /// </summary>
    private static List<string> ResolveTagDiscriminators(Internal.ArchetypeSignature signature, ComponentCodecRegistry registry)
    {
        var tags = new List<string>();
        foreach (var typeIndex in signature.SetBits)
            if (registry.TryGetTagByTypeIndex(typeIndex, out var discriminator))
                tags.Add(discriminator);
        return tags;
    }

    /// <summary>
    /// Every live entity, including ones with no registered components or tags — the
    /// case <see cref="EnumerateAll"/> silently drops, since it only visits archetypes by
    /// walking their registered component storages. Debug/tooling path, not a per-tick
    /// one. Same trimming guidance and eager-materialization reasoning as
    /// <see cref="EnumerateArchetypes"/> apply here too.
    /// </summary>
    public IReadOnlyList<EntitySnapshot> EnumerateEntities(ComponentCodecRegistry registry)
    {
        var result = new List<EntitySnapshot>();

        foreach (var archetype in _archetypes.Values)
        {
            if (archetype.Count == 0) continue;

            var tags = ResolveTagDiscriminators(archetype.Signature, registry);

            // Resolve which storages are registered once per archetype, not once per row
            // — the answer is identical for every entity in the archetype, so checking it
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
