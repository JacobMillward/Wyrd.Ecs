namespace Wyrd.Ecs;

/// <summary>
/// The engine's convenience entry point: create/destroy entities and add, remove, or
/// query components and tags, without needing to know what an archetype or chunk is.
/// Every mutable component accessor here is the tracked path — see the design's
/// Dirty-tracking section; there is no separate untracked accessor to bypass it.
/// </summary>
public interface IWorld
{
    /// <summary>Creates a new, empty entity.</summary>
    Entity CreateEntity();

    /// <summary>Destroys an entity and all of its components.</summary>
    void DestroyEntity(Entity entity);

    /// <summary>True if <paramref name="entity"/> refers to a live entity in this world.</summary>
    bool IsAlive(Entity entity);

    /// <summary>The permanent, opaque identity of <paramref name="entity"/> — see <see cref="EntityId"/>.</summary>
    EntityId GetPermanentId(Entity entity);

    /// <summary>
    /// Adds <typeparamref name="T"/> to <paramref name="entity"/> and returns a
    /// tracked mutable reference to it. Throws if the entity already has the component.
    /// </summary>
    ref T AddComponent<T>(Entity entity) where T : struct, IComponent;

    /// <summary>
    /// Returns a tracked mutable reference to <paramref name="entity"/>'s
    /// <typeparamref name="T"/>. Throws if the entity does not have the component.
    /// </summary>
    ref T GetComponent<T>(Entity entity) where T : struct, IComponent;

    /// <summary>Copies <paramref name="entity"/>'s <typeparamref name="T"/> without marking it dirty.</summary>
    bool TryGetComponent<T>(Entity entity, out T value) where T : struct, IComponent;

    /// <summary>True if <paramref name="entity"/> has a <typeparamref name="T"/> component.</summary>
    bool HasComponent<T>(Entity entity) where T : struct, IComponent;

    /// <summary>Removes <typeparamref name="T"/> from <paramref name="entity"/>, if present.</summary>
    void RemoveComponent<T>(Entity entity) where T : struct, IComponent;

    /// <summary>Adds tag <typeparamref name="T"/> to <paramref name="entity"/>.</summary>
    void AddTag<T>(Entity entity) where T : struct, ITag;

    /// <summary>Removes tag <typeparamref name="T"/> from <paramref name="entity"/>, if present.</summary>
    void RemoveTag<T>(Entity entity) where T : struct, ITag;

    /// <summary>True if <paramref name="entity"/> has tag <typeparamref name="T"/>.</summary>
    bool HasTag<T>(Entity entity) where T : struct, ITag;

    /// <summary>
    /// Hot-path query: invokes <paramref name="action"/> once per matching archetype
    /// chunk with a <typeparamref name="TAccess0"/> component accessor. The primary
    /// API for systems that run every tick over many entities — see
    /// <see cref="ChunkAction{TAccess0}"/>.
    /// </summary>
    void Query<TAccess0>(ChunkAction<TAccess0> action) where TAccess0 : struct, IComponentAccessor<TAccess0>;

    /// <summary>Two-component overload, using <see cref="ChunkAction{TAccess0, TAccess1}"/>.</summary>
    void Query<TAccess0, TAccess1>(ChunkAction<TAccess0, TAccess1> action)
        where TAccess0 : struct, IComponentAccessor<TAccess0>
        where TAccess1 : struct, IComponentAccessor<TAccess1>;

    /// <summary>
    /// Hidden-chunk convenience query: returns a <c>foreach</c>-able sequence of one
    /// <typeparamref name="TAccess0"/> accessor per matching entity, with no chunk or
    /// archetype vocabulary required. Supersedes the scaffold phase's
    /// <c>ForEach&lt;T0&gt;(EntityAction&lt;T0&gt;)</c> entirely — see
    /// <see cref="EntityQuery{TAccess0}"/>.
    /// </summary>
    EntityQuery<TAccess0> Query<TAccess0>() where TAccess0 : struct, IComponentAccessor<TAccess0>;
}
