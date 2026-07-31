namespace Wyrd.Ecs;

/// <summary>
/// The engine's convenience entry point: query components and tags, and read/mutate
/// an existing entity's component values, without needing to know what an archetype
/// or chunk is. Structural mutation — creating or destroying an entity, adding or
/// removing a component or tag — is not here: it always goes through
/// <see cref="Commands"/>, the only way to perform any of it. That's deliberate, not
/// a missing convenience — see <see cref="Wyrd.Ecs.CommandBuffer"/>'s own docs for why.
/// Every mutable component accessor here is the tracked path — see the design's
/// Dirty-tracking section; there is no separate untracked accessor to bypass it.
/// </summary>
public partial interface IWorld
{
    /// <summary>True if <paramref name="entity"/> refers to a live entity in this world.</summary>
    bool IsAlive(Entity entity);

    /// <summary>The permanent, opaque identity of <paramref name="entity"/> — see <see cref="EntityId"/>.</summary>
    EntityId GetPermanentId(Entity entity);

    /// <summary>
    /// The current tick, starting at 1 and advanced by <see cref="AdvanceTick"/>. Every
    /// tracked write stamps the row it touches with this value — see
    /// <see cref="TrackChanges{T}"/>/<see cref="ReadChanges{T}"/>.
    /// </summary>
    int CurrentTick { get; }

    /// <summary>Advances to the next tick.</summary>
    void AdvanceTick();

    /// <summary>The built-in deferred-mutation buffer for structural changes — see <see cref="Wyrd.Ecs.CommandBuffer"/>.</summary>
    CommandBuffer Commands { get; }

    /// <summary>
    /// Creates an additional <see cref="Wyrd.Ecs.CommandBuffer"/> bound to this world,
    /// independent of <see cref="Commands"/> and of every other buffer created this way.
    /// Each buffer is single-writer by construction — nothing is ever shared between
    /// them — so this is the mechanism for queuing structural changes safely from
    /// multiple concurrent sources (e.g. a future scheduler handing one buffer per
    /// system) without any locking: give each writer its own buffer, then apply them
    /// all, in whatever order the caller chooses, via <see cref="ApplyCommands(CommandBuffer)"/>.
    /// </summary>
    CommandBuffer CreateCommands();

    /// <summary>Applies every command queued on <see cref="Commands"/> since the last call, in queued order, then clears the queue.</summary>
    void ApplyCommands();

    /// <summary>
    /// Applies every command queued on <paramref name="commands"/> since its last apply,
    /// in queued order, then clears its queue. <paramref name="commands"/> may be
    /// <see cref="Commands"/> or any buffer returned by <see cref="CreateCommands"/> —
    /// the caller decides which buffer to apply and in what order, which is what makes
    /// replay order deterministic and reproducible regardless of how many buffers were
    /// written to concurrently beforehand. Throws if <paramref name="commands"/> was
    /// created for a different <see cref="IWorld"/>.
    /// </summary>
    void ApplyCommands(CommandBuffer commands);

    /// <summary>
    /// Returns a tracked mutable reference to <paramref name="entity"/>'s
    /// <typeparamref name="T"/>. Throws if the entity does not have the component.
    /// <para>
    /// <b>Ref lifetime:</b> do not hold the returned reference across a call to
    /// <see cref="ApplyCommands()"/> (or the <see cref="ApplyCommands(CommandBuffer)"/>
    /// overload). A structural change applied afterward can silently invalidate it — a
    /// component ref can end up aliasing a different entity's data after a swap-remove.
    /// This is not detectable at the point of misuse. Read or write the reference
    /// immediately, then let it go out of scope.
    /// </para>
    /// </summary>
    ref T GetComponent<T>(Entity entity) where T : struct, IComponent;

    /// <summary>
    /// A non-storable, <see cref="World"/>-scoped bound view over <paramref name="entity"/>
    /// — see <see cref="EntityView"/>.
    /// </summary>
    EntityView this[Entity entity] { get; }

    /// <summary>
    /// Returns a tracked mutable reference to <paramref name="entity"/>'s
    /// <typeparamref name="T"/>, with <paramref name="found"/> <see langword="true"/>, if
    /// the entity has the component; otherwise <paramref name="found"/> is
    /// <see langword="false"/> and the returned reference must not be dereferenced (doing
    /// so throws <see cref="NullReferenceException"/>). Same tracked-ref contract as
    /// <see cref="GetComponent{T}(Entity)"/> — a <paramref name="found"/> flag instead of
    /// a throw, nothing else different, including the same ref-lifetime caveat: do not
    /// hold the returned reference across a call to <see cref="ApplyCommands()"/>.
    /// </summary>
    ref T TryGetComponent<T>(Entity entity, out bool found) where T : struct, IComponent;

    /// <summary>True if <paramref name="entity"/> has a <typeparamref name="T"/> component.</summary>
    bool HasComponent<T>(Entity entity) where T : struct, IComponent;

    /// <summary>True if <paramref name="entity"/> has tag <typeparamref name="T"/>.</summary>
    bool HasTag<T>(Entity entity) where T : struct, ITag;

    /// <summary>True if <paramref name="source"/> has a <typeparamref name="T"/> edge to <paramref name="target"/>.</summary>
    bool HasRelation<T>(Entity source, Entity target) where T : struct, IRelation;

    /// <summary>Copies the payload of <paramref name="source"/>'s <typeparamref name="T"/> edge to <paramref name="target"/>, if it exists.</summary>
    bool TryGetRelation<T>(Entity source, Entity target, out T value) where T : struct, IRelation;

    /// <summary>Every target <paramref name="source"/> has a <typeparamref name="T"/> edge to, and each edge's payload. Empty, not throwing, if <paramref name="source"/> has none. O(fan-out) to enumerate, not O(1) — see <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/>'s doc for what is O(1).</summary>
    IReadOnlyDictionary<Entity, T> Targets<T>(Entity source) where T : struct, IRelation;

    /// <summary>Every source entity with a <typeparamref name="T"/> edge pointing at <paramref name="target"/>. Empty, not throwing, if none. O(fan-out) to enumerate.</summary>
    IReadOnlyCollection<Entity> Sources<T>(Entity target) where T : struct, IRelation;

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
    /// Registers <paramref name="observer"/> to be notified of every structural change
    /// from this point on. Dispose the returned handle to unregister.
    /// </summary>
    IDisposable ObserveStructuralChanges(IStructuralChangeObserver observer);

    /// <summary>
    /// Scans every archetype containing <typeparamref name="T"/> for rows whose
    /// tick-stamp is past <paramref name="sinceTick"/>, returning each one's current
    /// value. Stateless and non-destructive — call this as many times, from as many
    /// independent readers with their own watermark, as needed. Only observes rows
    /// touched while <see cref="TrackChanges{T}"/> was registered for this type.
    /// </summary>
    ChangedComponents<T> ReadChanges<T>(int sinceTick) where T : struct, IComponent;

    /// <summary>
    /// Walks every live entity and every one of its components that has a registration
    /// in <paramref name="registry"/>, yielding one <see cref="EncodedComponent"/>
    /// per (entity, registered component type) pair. Unregistered component types and
    /// all tags are skipped — tags carry no data, so there's nothing to serialize. A
    /// pure read over existing archetype storage: it never moves a row, so unlike
    /// structural mutation it carries none of the hazards <see cref="Commands"/> exists
    /// for, and needs none of its deferral. Returns a plain, allocating
    /// <see cref="IEnumerable{T}"/> rather than the ref-struct enumerators
    /// <c>Query</c> uses — this is a full-world snapshot walk, called
    /// rarely (a save, a checkpoint, a replica sync), not a per-tick hot path, and its
    /// consumer (a serialization pipeline that wants to filter, transform, or stream the
    /// result) needs the composability a ref struct can't offer; the one iterator
    /// allocation for the whole walk is negligible next to the per-component byte[]
    /// allocations <see cref="IComponentCodec.EncodeRow"/> already does.
    /// </summary>
    IEnumerable<EncodedComponent> EnumerateAll(ComponentCodecRegistry registry);
}
