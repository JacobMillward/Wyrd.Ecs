namespace Wyrd.Ecs;

/// <summary>
/// The forward side of a data-carrying relationship: every target entity the owning
/// entity has a <typeparamref name="T"/> edge to, and that edge's payload. An ordinary
/// component — it occupies one archetype signature bit regardless of how many targets
/// it holds; adding or removing a specific target mutates <see cref="Targets"/> in
/// place and never moves an archetype row on its own. Only ever constructed through
/// <see cref="CommandBuffer.AddRelation{T}"/> (via <see cref="World"/>'s internal
/// get-or-create helper), which always initializes the backing dictionary — a
/// default-constructed instance has a null backing store and exists only to satisfy
/// <see cref="Internal.ComponentStorage{T}"/>'s zero-init on grow, not for direct use.
/// </summary>
public readonly struct RelationLinks<T> : IComponent where T : struct, IComponent
{
    private readonly Dictionary<Entity, T>? _targets;

    internal RelationLinks(Dictionary<Entity, T> targets) => _targets = targets;

    /// <summary>The live, mutable backing store — internal so mutation only ever happens through <see cref="CommandBuffer"/>, never directly from a query body.</summary>
    internal Dictionary<Entity, T>? Targets => _targets;

    /// <summary>Every target this entity has a <typeparamref name="T"/> edge to, and that edge's payload. Read-only — see this type's own doc for why.</summary>
    public IReadOnlyDictionary<Entity, T> Values => _targets!;
}
