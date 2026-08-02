namespace Wyrd.Ecs;

/// <summary>
/// The forward side of a relationship: every target entity the owning entity has a
/// <typeparamref name="T"/> edge to, and that edge's payload (an empty
/// <typeparamref name="T"/> struct for a marker-only relation). An ordinary component:
/// it occupies one archetype signature bit regardless of target count; adding or
/// removing a target mutates <see cref="Targets"/> in place and never moves an
/// archetype row. Only ever constructed through
/// <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/>, which always
/// initializes the backing dictionary; a default-constructed instance exists only to
/// satisfy <see cref="Internal.ComponentStorage{T}"/>'s zero-init on grow.
/// </summary>
public readonly struct RelationLinks<T> : IComponent where T : struct, IRelation
{
    private readonly Dictionary<Entity, T>? _targets;

    internal RelationLinks(Dictionary<Entity, T> targets) => _targets = targets;

    /// <summary>The live, mutable backing store. Internal so mutation only ever happens through <see cref="CommandBuffer"/>, never directly from a query body.</summary>
    internal Dictionary<Entity, T>? Targets => _targets;

    /// <summary>Every target this entity has a <typeparamref name="T"/> edge to, and that edge's payload. Read-only; see this type's own doc for why.</summary>
    public IReadOnlyDictionary<Entity, T> Values => _targets!;

    static RelationLinks() => Internal.RelationRegistry.Register(Internal.TypeIndex<RelationLinks<T>>.Value, CascadeRemove);

    /// <summary>Removes this entity from every one of its targets' <see cref="RelationBacklinks{T}"/>. This component's own row is about to be deleted wholesale as part of the same destroy, so only the mirror needs cleaning here.</summary>
    private static void CascadeRemove(World world, Entity self, Internal.IComponentStorage storage, int row)
    {
        var links = ((Internal.ComponentStorage<RelationLinks<T>>)storage)[row];
        foreach (var target in links.Targets!.Keys)
            world.RemoveRelationBacklink<T>(target, self);
    }
}
