using System.Collections.Generic;

namespace Wyrd.Ecs;

/// <summary>
/// Full-world enumeration walks for save/checkpoint: one encoded record per live
/// (entity, registered type) pair, plus relation edges. Not a per-tick path.
/// </summary>
public sealed partial class World
{
    /// <summary>Yields one <see cref="EncodedComponent"/> per (entity, registered component type) pair for every live entity. Unregistered types and tags are skipped. A full-world walk, for a save/checkpoint, not a per-tick path.</summary>
    public IEnumerable<EncodedComponent> EnumerateAll(CodecRegistry registry)
    {
        foreach (var archetype in _archetypes.Values)
        {
            if (archetype.Count == 0) continue;

            foreach (var (typeIndex, storage) in archetype.Storages)
            {
                if (!registry.TryGetByTypeIndex(typeIndex, out var registered)) continue;

                for (var row = 0; row < archetype.Count; row++)
                    yield return new EncodedComponent(archetype.Entities[row], registered.Discriminator, registered.SchemaHash, registered.EncodeRow(storage.RawItems, row));
            }
        }
    }

    /// <summary>
    /// Every live relation edge whose payload type is registered in <paramref name="registry"/>
    /// via <see cref="CodecRegistry.RegisterRelation{T}"/>, one <see cref="EncodedRelation"/>
    /// per edge. Mirrors <see cref="EnumerateAll"/>, walking <see cref="RelationLinks{T}"/>
    /// storages instead of ordinary component storages. <see cref="RelationBacklinks{T}"/>
    /// is never walked here: replaying an edge through
    /// <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/> regenerates it as a
    /// side effect, same as at runtime.
    /// </summary>
    public IEnumerable<EncodedRelation> EnumerateRelations(CodecRegistry registry)
    {
        foreach (var archetype in _archetypes.Values)
        {
            if (archetype.Count == 0) continue;

            foreach (var (typeIndex, storage) in archetype.Storages)
            {
                if (!registry.TryGetRelationByLinksTypeIndex(typeIndex, out var registered)) continue;

                for (var row = 0; row < archetype.Count; row++)
                {
                    var source = archetype.Entities[row];
                    foreach (var (target, payload) in registered.EncodeRow(storage.RawItems, row))
                        yield return new EncodedRelation(source, target, registered.Discriminator, registered.SchemaHash, payload);
                }
            }
        }
    }

    /// <summary>Every live (entity, registered tag) pair. Unregistered tags are skipped, same "unregistered means absent" contract <see cref="EnumerateAll"/> already has for components.</summary>
    public IEnumerable<EncodedTag> EnumerateAllTags(CodecRegistry registry)
    {
        foreach (var archetype in _archetypes.Values)
        {
            if (archetype.Count == 0) continue;

            foreach (var typeIndex in archetype.Signature.SetBits)
            {
                if (!registry.TryGetTagByTypeIndex(typeIndex, out var binder)) continue;

                for (var row = 0; row < archetype.Count; row++)
                    yield return new EncodedTag(archetype.Entities[row], binder.Discriminator);
            }
        }
    }
}
