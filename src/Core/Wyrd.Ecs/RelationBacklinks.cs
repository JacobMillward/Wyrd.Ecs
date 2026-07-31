namespace Wyrd.Ecs;

/// <summary>
/// The reverse side of a data-carrying relationship: every source entity pointing a
/// <typeparamref name="T"/> edge at the owning entity. Carries no payload — only
/// <see cref="RelationLinks{T}"/>, on the source side, does — this type exists purely
/// so entity-destroy cascade cleanup (see the design doc's "Destroy cascade" section)
/// can find "who points at me" in O(1) instead of scanning every entity in the world.
/// Same construction/mutation discipline as <see cref="RelationLinks{T}"/> — see its doc.
/// </summary>
public readonly struct RelationBacklinks<T> : IComponent where T : struct, IComponent
{
    private readonly HashSet<Entity>? _sources;

    internal RelationBacklinks(HashSet<Entity> sources) => _sources = sources;

    /// <summary>The live, mutable backing store — internal, same reasoning as <see cref="RelationLinks{T}.Targets"/>.</summary>
    internal HashSet<Entity>? Sources => _sources;

    /// <summary>Every source entity with a <typeparamref name="T"/> edge pointing at this entity. Read-only.</summary>
    public IReadOnlyCollection<Entity> Values => _sources!;

    static RelationBacklinks() => Internal.RelationRegistry.Register(Internal.TypeIndex<RelationBacklinks<T>>.Value, CascadeRemove);

    /// <summary>Removes this entity from every one of its sources' <see cref="RelationLinks{T}"/> — the mirror of <see cref="RelationLinks{T}"/>'s own cascade.</summary>
    private static void CascadeRemove(World world, Entity self, Internal.IComponentStorage storage, int row)
    {
        var backlinks = ((Internal.ComponentStorage<RelationBacklinks<T>>)storage)[row];
        foreach (var source in backlinks.Sources!)
            world.RemoveRelationLink<T>(source, self);
    }
}
