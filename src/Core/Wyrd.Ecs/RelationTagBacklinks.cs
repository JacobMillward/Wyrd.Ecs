namespace Wyrd.Ecs;

/// <summary>
/// The reverse side of a tag relationship: every source entity pointing a
/// <typeparamref name="T"/> edge at the owning entity. Same reasoning as
/// <see cref="RelationBacklinks{T}"/>, just for the tag-relation family.
/// </summary>
public readonly struct RelationTagBacklinks<T> : IComponent where T : struct, ITag
{
    private readonly HashSet<Entity>? _sources;

    internal RelationTagBacklinks(HashSet<Entity> sources) => _sources = sources;

    internal HashSet<Entity>? Sources => _sources;

    /// <summary>Every source entity with a <typeparamref name="T"/> edge pointing at this entity. Read-only.</summary>
    public IReadOnlyCollection<Entity> Values => _sources!;

    static RelationTagBacklinks() => Internal.RelationRegistry.Register(Internal.TypeIndex<RelationTagBacklinks<T>>.Value, CascadeRemove);

    private static void CascadeRemove(World world, Entity self, Internal.IComponentStorage storage, int row)
    {
        var backlinks = ((Internal.ComponentStorage<RelationTagBacklinks<T>>)storage)[row];
        foreach (var source in backlinks.Sources!)
            world.RemoveRelationTagLink<T>(source, self);
    }
}
