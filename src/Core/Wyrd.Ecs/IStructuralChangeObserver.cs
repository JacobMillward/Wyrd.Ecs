namespace Wyrd.Ecs;

/// <summary>
/// Observes structural changes (entity creation/destruction, component/tag add/remove
/// on an existing entity) as they actually happen, whether applied immediately via a
/// direct <see cref="World"/> call or deferred through <see cref="CommandBuffer"/> and
/// applied later. Register via <see cref="World.ObserveStructuralChanges"/>.
/// <c>typeIndex</c> parameters are <see cref="Internal.TypeIndex{T}"/> values, the same
/// runtime-only, per-process index the engine itself uses, not a value safe to persist
/// across a restart.
/// </summary>
public interface IStructuralChangeObserver
{
    /// <summary>An entity was created, with whatever initial components it was given — never followed by <see cref="OnComponentAdded"/> for those same initial components.</summary>
    void OnEntityCreated(Entity entity);

    /// <summary>An entity was destroyed, along with all of its components.</summary>
    void OnEntityDestroyed(Entity entity);

    /// <summary>A component was added to an already-existing entity.</summary>
    void OnComponentAdded(Entity entity, int typeIndex);

    /// <summary>A component was removed from an entity.</summary>
    void OnComponentRemoved(Entity entity, int typeIndex);

    /// <summary>A tag was added to an entity.</summary>
    void OnTagAdded(Entity entity, int typeIndex);

    /// <summary>A tag was removed from an entity.</summary>
    void OnTagRemoved(Entity entity, int typeIndex);
}
