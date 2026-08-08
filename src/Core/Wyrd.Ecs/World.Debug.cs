namespace Wyrd.Ecs;

public sealed partial class World
{
    /// <summary>
    /// Every archetype with at least one live entity: its entity count and the debug name
    /// of every component/tag type present, resolved via
    /// <see cref="Internal.DebugNameRegistry"/> - no registry parameter, no setup, works
    /// for any type the generated module initializer discovered.  Eagerly materialized
    /// into a list, not lazily enumerated, since a lazily-suspended enumerator left open
    /// across a structural mutation would throw <see cref="InvalidOperationException"/>.
    /// Debug/tooling path, not a per-tick one; to keep a call site out of a
    /// trimmed/Native AOT Release publish, wrap it in <c>#if DEBUG</c>/
    /// <c>[Conditional("DEBUG")]</c> in your own project.
    /// </summary>
    public IReadOnlyList<ArchetypeSnapshot> EnumerateArchetypes()
    {
        var result = new List<ArchetypeSnapshot>();

        foreach (var archetype in _archetypes.Values)
        {
            if (archetype.Count == 0) continue;

            SplitComponentsAndTags(archetype, out var componentNames, out var tags);
            result.Add(new ArchetypeSnapshot(archetype.Count, componentNames.Select(c => c.Name).ToList(), tags));
        }

        return result;
    }

    /// <summary>
    /// Every live entity, by debug name only - no byte payloads, no registry needed. A
    /// component appears with an empty <see cref="EncodedComponent.Data"/> rather than
    /// being omitted, unlike the registry-taking overload, which attaches real encoded
    /// bytes for any type that also has a registered codec. Same eager-materialization
    /// and trimming guidance as <see cref="EnumerateArchetypes()"/>.
    /// </summary>
    public IReadOnlyList<EntitySnapshot> EnumerateEntities()
    {
        var result = new List<EntitySnapshot>();

        foreach (var archetype in _archetypes.Values)
        {
            if (archetype.Count == 0) continue;

            SplitComponentsAndTags(archetype, out var componentNames, out var tags);

            for (var row = 0; row < archetype.Count; row++)
            {
                var components = componentNames.Select(c => new EncodedComponent(archetype.Entities[row], c.Name, null, [])).ToList();
                result.Add(new EntitySnapshot(archetype.Entities[row], components, tags));
            }
        }

        return result;
    }

    /// <summary>
    /// Every live entity, including ones with no registered components or tags: the case
    /// <see cref="EnumerateAll"/> silently drops, since it only visits archetypes by
    /// walking their registered component storages. Names come from
    /// <see cref="Internal.DebugNameRegistry"/>, same as the zero-arg overload; real
    /// <see cref="EncodedComponent.Data"/> is attached wherever <paramref name="registry"/>
    /// also has a codec for that type, empty otherwise - never omitted. Same trimming
    /// guidance and eager-materialization reasoning as <see cref="EnumerateArchetypes()"/>.
    /// </summary>
    public IReadOnlyList<EntitySnapshot> EnumerateEntities(CodecRegistry registry)
    {
        var result = new List<EntitySnapshot>();

        foreach (var archetype in _archetypes.Values)
        {
            if (archetype.Count == 0) continue;

            SplitComponentsAndTags(archetype, out var componentNames, out var tags);

            for (var row = 0; row < archetype.Count; row++)
            {
                var components = new List<EncodedComponent>(componentNames.Count);
                foreach (var (typeIndex, name) in componentNames)
                {
                    var data = registry.TryGetByTypeIndex(typeIndex, out var codec)
                        ? codec.EncodeRow(archetype.Storages[typeIndex].RawItems, row)
                        : [];
                    components.Add(new EncodedComponent(archetype.Entities[row], name, null, data));
                }

                result.Add(new EntitySnapshot(archetype.Entities[row], components, tags));
            }
        }

        return result;
    }

    /// <summary>
    /// Splits <paramref name="archetype"/>'s signature bits into component and tag debug
    /// names, resolved once per archetype rather than once per entity row, since the
    /// answer is identical for every entity in the same archetype. A bit distinguishes
    /// component from tag by whether the archetype has backing storage for it - a tag
    /// never does. A bit with no <see cref="Internal.DebugNameRegistry"/> entry is
    /// silently skipped.
    /// </summary>
    private static void SplitComponentsAndTags(Internal.Archetype archetype, out List<(int TypeIndex, string Name)> components, out List<string> tags)
    {
        components = [];
        tags = [];
        foreach (var typeIndex in archetype.Signature.SetBits)
        {
            if (!Internal.DebugNameRegistry.TryGetName(typeIndex, out var name)) continue;
            if (archetype.Storages.TryGetValue(typeIndex, out _)) components.Add((typeIndex, name));
            else tags.Add(name);
        }
    }
}
