namespace Wyrd.Ecs;

/// <summary>
/// The reverse side of a relationship: every source entity pointing a
/// <typeparamref name="T"/> edge at the owning entity. Carries no payload; only
/// <see cref="RelationLinks{T}"/>, on the source side, does. Lets destroy-cascade cleanup
/// find "who points at me" in O(1) instead of scanning every entity. Same
/// construction/mutation discipline as <see cref="RelationLinks{T}"/>.
/// </summary>
internal readonly struct RelationBacklinks<T> : IComponent where T : struct, IRelation
{
    private readonly HashSet<Entity>? _sources;

    internal RelationBacklinks(HashSet<Entity> sources) => _sources = sources;

    /// <summary>The live, mutable backing store. Internal, same reasoning as <see cref="RelationLinks{T}.Targets"/>.</summary>
    internal HashSet<Entity>? Sources => _sources;

    /// <summary>Every source entity with a <typeparamref name="T"/> edge pointing at this entity. Read-only.</summary>
    public IReadOnlyCollection<Entity> Values => _sources!;

    static RelationBacklinks() => Internal.RelationRegistry.Register(Internal.TypeIndex<RelationBacklinks<T>>.Value, CascadeRemove);

    /// <summary>
    /// For a non-<see cref="IDependent"/> relation: removes this entity from every
    /// source's <see cref="RelationLinks{T}"/>. For an <see cref="IDependent"/> relation
    /// (e.g. <see cref="Parent"/>): recursively destroys every source instead, so
    /// destroying the target of a hierarchy edge despawns the whole subtree. Sources are
    /// snapshotted to an array first since destroying one re-enters and removes it from
    /// this same live set, which would otherwise mutate the collection mid-enumeration.
    /// </summary>
    private static void CascadeRemove(World world, Entity self, Internal.IComponentStorage storage, int row)
    {
        var backlinks = ((Internal.ComponentStorage<RelationBacklinks<T>>)storage)[row];
        if (Internal.RelationTraits<T>.IsDependent)
        {
            foreach (var source in backlinks.Sources!.ToArray())
                if (world.IsAlive(source)) world.DestroyEntity(source);
        }
        else
        {
            foreach (var source in backlinks.Sources!)
            {
                world.RemoveRelationLink<T>(source, self);
                world.NotifyRelationUnlinked(source, self, Internal.TypeIndex<T>.Value);
            }
        }
    }
}
