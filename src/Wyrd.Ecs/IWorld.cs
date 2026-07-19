namespace Wyrd.Ecs;

/// <summary>
/// The engine's convenience entry point: create/destroy entities and add, remove, or
/// query components and tags, without needing to know what an archetype or chunk is.
/// Every mutable component accessor here is the tracked path — see the design's
/// Dirty-tracking section; there is no separate untracked accessor to bypass it.
/// </summary>
public partial interface IWorld
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
    /// The current tick, starting at 1 and advanced by <see cref="AdvanceTick"/>. Used
    /// by the tracked mutation path's per-tick dedup — see the design's Dirty-tracking
    /// section: an entity touched many times in one tick produces one change-log entry,
    /// not many.
    /// </summary>
    int CurrentTick { get; }

    /// <summary>Advances to the next tick.</summary>
    void AdvanceTick();

    /// <summary>The deferred-mutation buffer for structural changes — see <see cref="Wyrd.Ecs.Commands"/>.</summary>
    Commands Commands { get; }

    /// <summary>Applies every command queued on <see cref="Commands"/> since the last call, in queued order, then clears the queue.</summary>
    void ApplyCommands();

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

    /// <summary>
    /// A non-storable, <see cref="World"/>-scoped bound view over <paramref name="entity"/>
    /// — see <see cref="EntityView"/>.
    /// </summary>
    EntityView this[Entity entity] { get; }

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
    void Query<TAccess0>(ChunkAction<TAccess0> action) where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct;

    /// <summary>Two-component overload, using <see cref="ChunkAction{TAccess0, TAccess1}"/>.</summary>
    void Query<TAccess0, TAccess1>(ChunkAction<TAccess0, TAccess1> action)
        where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
        where TAccess1 : struct, IComponentAccessor<TAccess1>, allows ref struct;

    // Query<T0..T{QueryArity.Max-1}>() (the unified entity-tier query, replacing
    // QueryMut<T>/QueryRef<T> outright) is generated — see
    // src/Wyrd.Ecs.Generators/WorldQueryMembersGenerator.cs.

    /// <summary>
    /// Turns change tracking on for <typeparamref name="T"/>. Returns a handle; dispose
    /// it to turn tracking back off if this was the last registration for the type.
    /// This is the only way to make <see cref="ReadChanges{T}"/> observe anything —
    /// while untracked, writes to <typeparamref name="T"/> never stamp a tick.
    /// </summary>
    IDisposable TrackChanges<T>() where T : struct, IComponent;

    /// <summary>
    /// Scans every archetype containing <typeparamref name="T"/> for rows whose
    /// tick-stamp is past <paramref name="sinceTick"/>, returning each one's current
    /// value. Stateless and non-destructive — call this as many times, from as many
    /// independent readers with their own watermark, as needed. Only observes rows
    /// touched while <see cref="TrackChanges{T}"/> was registered for this type.
    /// </summary>
    ChangedComponents<T> ReadChanges<T>(int sinceTick) where T : struct, IComponent;
}
