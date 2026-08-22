using System.Collections.Generic;
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// Entity identity and lifecycle: the id table, liveness checks, reservation/placement
/// (the placement half of <see cref="CommandBuffer"/>'s queued creation), and destruction
/// with its reentrancy guard and relation teardown.
/// </summary>
public sealed partial class World
{
    private EntityTable _entityTable = new();

    // Ids currently inside DestroyEntity, as an explicit stack: dependent-relation cascades
    // and observers destroy nested entities synchronously, and a reentrant hit on any of
    // them must no-op (see DestroyEntity). Structural mutation is single-threaded by
    // contract - CommandBuffer.Apply documents "only ever called single-threaded" - so a
    // plain List needs no synchronization. A List beats HashSet/bitset at realistic cascade
    // depths: flat destroys short-circuit on Count == 0, and nesting depth is tree height,
    // usually single digits.
    private readonly List<int> _destroyingIds = new();

    /// <summary>
    /// Destroys an entity and all its components. Called only via <see cref="CommandBuffer"/>.
    /// Notifies observers before removing the entity. Unlike every other structural
    /// notification (which fires after the change lands), destruction leaves nothing
    /// queryable afterward, so "before" is the only point an observer can still read
    /// anything about the entity.
    /// Reentrant: destroying an entity that is already mid-destroy (an observer or a
    /// dependent-relation cascade reaching it before its row is removed, when every other
    /// liveness check still passes) is a no-op - the in-flight outer frame completes the
    /// destroy exactly once. This is what makes hierarchy cycles unwind instead of recursing
    /// to a stack overflow. Single-threaded like every structural mutation; see <see cref="_destroyingIds"/>.
    /// </summary>
    internal void DestroyEntity(Entity entity)
    {
        RequireAlive(entity);
        var id = entity.Id;
        if (_destroyingIds.Contains(id)) return;
        _destroyingIds.Add(id);
        try
        {
            NotifyEntityDestroyed(entity);
            CascadeRemoveRelations(entity);
            _entityTable.Destroy(id);
        }
        finally
        {
            _destroyingIds.RemoveAt(_destroyingIds.Count - 1);
        }
    }

    /// <summary>
    /// Cleans up every relation edge touching <paramref name="entity"/>, in both directions,
    /// before its row is removed. Re-resolves <paramref name="entity"/>'s location before each
    /// type's cascade step rather than just once upfront, since a self-relation can have one step's
    /// cleanup remove a different relation component of the same entity as a side effect (an
    /// archetype move), which would invalidate a location captured earlier.
    /// </summary>
    private void CascadeRemoveRelations(Entity entity)
    {
        var initialLocation = _entityTable[entity.Id];

        // Lazily allocated: zero-cost for the common case of no relation components. Runs on every destroy.
        List<int>? relationTypeIndices = null;
        foreach (var (typeIndex, _) in initialLocation.Archetype.Storages)
        {
            if (RelationRegistry.Get(typeIndex) is not null)
                (relationTypeIndices ??= new List<int>()).Add(typeIndex);
        }
        if (relationTypeIndices is null) return;

        foreach (var typeIndex in relationTypeIndices)
        {
            var handler = RelationRegistry.Get(typeIndex);
            if (handler is null) continue;

            var current = _entityTable[entity.Id];
            if (!current.Archetype.Signature.Contains(typeIndex)) continue; // already cleaned up as a side effect above

            current.Archetype.Storages.TryGetValue(typeIndex, out var storage);
            handler(this, entity, storage, current.Row);
        }
    }

    /// <summary>True if <paramref name="entity"/> refers to a live entity in this world.</summary>
    public bool IsAlive(Entity entity) => _entityTable.IsAlive(entity.Id, entity.Generation);

    /// <summary>The permanent, opaque identity of <paramref name="entity"/>. See <see cref="EntityId"/>.</summary>
    public EntityId GetPermanentId(Entity entity)
    {
        RequireAlive(entity);
        return _entityTable.PermanentId(entity.Id);
    }

    /// <summary>Reserves a fresh entity id without placing it. See <see cref="Internal.EntityTable.Reserve"/>. Used only by <see cref="CommandBuffer.CreateEntity()"/>.</summary>
    internal Entity ReserveEntity() => _entityTable.Reserve();

    /// <summary>Bulk counterpart to <see cref="ReserveEntity"/>. Used by <see cref="CommandBuffer.CreateEntity(int)"/> and its component-carrying siblings.</summary>
    internal void ReserveEntityRange(Span<Entity> destination) => _entityTable.ReserveRange(destination);

    /// <summary>Places a previously-reserved entity into the empty archetype, making it alive, and notifies observers. Used only by <see cref="CommandBuffer.CreateEntity()"/>'s queued placement.</summary>
    internal void PlaceReservedEntity(Entity entity)
    {
        _entityTable.Place(entity, _emptyArchetype);
        NotifyEntityCreated(entity);
    }

    /// <summary>
    /// Places a previously-reserved entity directly into the archetype matching
    /// <paramref name="signature"/>, creating it if needed, then invokes every setter in
    /// <paramref name="setters"/> against the placed row with <c>count = 1</c>. The
    /// <see cref="EntityTemplate"/> counterpart of the generated
    /// <c>PlaceReservedEntity&lt;T0..Tn&gt;</c> family, working from a pre-built
    /// signature/setter list instead of a closed generic.
    /// </summary>
    internal void PlaceReservedEntityFromTemplate(Entity entity, Internal.TypeBitSet signature, TemplateComponentSetter[] setters)
    {
        if (!_archetypes.TryGetValue(signature, out var target))
            target = CreateArchetype(signature);

        var row = _entityTable.Place(entity, target);
        NotifyEntityCreated(entity);

        // A concrete array parameter, not IReadOnlyCollection<T>: foreach over an
        // interface-typed collection here would force a boxed, virtually-dispatched
        // enumerator on every instantiate call, measured to roughly double this method's
        // cost relative to the generated PlaceReservedEntity<T0..Tn> family it mirrors. See
        // EntityTemplate.Setters' own doc for the matching fix on the producing side.
        foreach (var setter in setters)
            setter(this, target, row, 1);
    }

    /// <summary>
    /// Batch counterpart of <see cref="PlaceReservedEntityFromTemplate"/>: places every
    /// entity into the archetype matching <paramref name="signature"/>, creating it if
    /// needed, then invokes every setter once for the whole batch (each setter blits via
    /// <see cref="Internal.ComponentStorage{T}.Fill"/>) rather than once per entity.
    /// </summary>
    internal void PlaceReservedEntitiesFromTemplate(Entity[] entities, Internal.TypeBitSet signature, TemplateComponentSetter[] setters)
    {
        if (!_archetypes.TryGetValue(signature, out var target))
            target = CreateArchetype(signature);

        var startRow = target.AddRows(entities);
        _entityTable.PlaceBatch(entities, target, startRow);

        foreach (var setter in setters)
            setter(this, target, startRow, entities.Length);

        foreach (var entity in entities) NotifyEntityCreated(entity);
    }

    /// <summary>Bulk counterpart to <see cref="PlaceReservedEntity(Entity)"/>: places every entity in one <see cref="Internal.Archetype.AddRows"/> call. Used only by <see cref="CommandBuffer.CreateEntity(int)"/>'s queued placement.</summary>
    internal void PlaceReservedEntities(Entity[] entities)
    {
        var startRow = _emptyArchetype.AddRows(entities);
        _entityTable.PlaceBatch(entities, _emptyArchetype, startRow);
        foreach (var entity in entities) NotifyEntityCreated(entity);
    }

    private void RequireAlive(Entity entity)
    {
        if (!IsAlive(entity))
            throw new InvalidOperationException($"Entity {entity} is not alive.");
    }

    /// <summary>Resolves <paramref name="entity"/>'s current location in one entity-table read, or false if it isn't alive.</summary>
    internal bool TryResolve(Entity entity, out EntityLocation location)
    {
        if (!IsAlive(entity)) { location = default; return false; }
        location = _entityTable[entity.Id];
        return true;
    }
}
