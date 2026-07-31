namespace Wyrd.Ecs;

/// <summary>
/// The forward side of a relationship: every target entity the owning entity has a
/// <typeparamref name="T"/> edge to, and that edge's payload (an empty
/// <typeparamref name="T"/> struct, for a marker-only relation — see <see cref="IRelation"/>'s
/// own doc for why there's no separate tag-relation type). An ordinary component — it
/// occupies one archetype signature bit regardless of how many targets it holds; adding
/// or removing a specific target mutates <see cref="Targets"/> in place and never moves
/// an archetype row on its own. Only ever constructed through
/// <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/> (via <see cref="World"/>'s
/// internal get-or-create helper), which always initializes the backing dictionary — a
/// default-constructed instance has a null backing store and exists only to satisfy
/// <see cref="Internal.ComponentStorage{T}"/>'s zero-init on grow, not for direct use.
/// </summary>
public readonly struct RelationLinks<T> : IComponent where T : struct, IRelation
{
    private readonly Dictionary<Entity, T>? _targets;

    internal RelationLinks(Dictionary<Entity, T> targets) => _targets = targets;

    /// <summary>The live, mutable backing store — internal so mutation only ever happens through <see cref="CommandBuffer"/>, never directly from a query body.</summary>
    internal Dictionary<Entity, T>? Targets => _targets;

    /// <summary>Every target this entity has a <typeparamref name="T"/> edge to, and that edge's payload. Read-only — see this type's own doc for why.</summary>
    public IReadOnlyDictionary<Entity, T> Values => _targets!;

    static RelationLinks() => Internal.RelationRegistry.Register(Internal.TypeIndex<RelationLinks<T>>.Value, CascadeRemove);

    /// <summary>Removes this entity from every one of its targets' <see cref="RelationBacklinks{T}"/> — this component's own row is about to be deleted wholesale as part of the same destroy, so only the mirror needs cleaning here.</summary>
    private static void CascadeRemove(World world, Entity self, Internal.IComponentStorage storage, int row)
    {
        var links = ((Internal.ComponentStorage<RelationLinks<T>>)storage)[row];
        foreach (var target in links.Targets!.Keys)
            world.RemoveRelationBacklink<T>(target, self);
    }
}
