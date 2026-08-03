using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// Archetype-storage engine: entities with identical component/tag sets share one
/// <see cref="Archetype"/>; adding or removing a component/tag moves the entity to a
/// different archetype.
/// </summary>
public sealed partial class World
{
    /// <summary>
    /// Default floor for a new archetype's dense arrays when <see cref="WorldBuilder.WithArchetypeCapacity"/>
    /// wasn't used. Moderate and workload-agnostic: big enough to skip early doubling
    /// steps without assuming a large-few-archetypes shape.
    /// </summary>
    internal const int DefaultArchetypeCapacity = 64;

    private readonly Dictionary<ArchetypeSignature, Archetype> _archetypes = new();
    private readonly Dictionary<ArchetypeSignature, Archetype[]> _queryCache = new();
    private readonly Dictionary<(ArchetypeSignature Required, ArchetypeFilter Filter), Archetype[]> _filteredQueryCache = new();
    private readonly Archetype _emptyArchetype;
    private readonly int _archetypeCapacity;
    private readonly CommandBuffer _commands;

    private EntityTable _entityTable = new();
    private int _currentTick = 1;

    private readonly ISystemScheduler _executor;
    private TimeSpan _totalElapsed;

    /// <summary>Creates a new, empty world with <see cref="DefaultArchetypeCapacity"/>. Use <see cref="WorldBuilder"/> to configure it.</summary>
    public World() : this(DefaultArchetypeCapacity, new ParallelSystemScheduler(1000)) { }

    internal World(int archetypeCapacity, ISystemScheduler executor)
    {
        _archetypeCapacity = archetypeCapacity;
        _emptyArchetype = new Archetype(ArchetypeSignature.Empty, archetypeCapacity);
        _archetypes[ArchetypeSignature.Empty] = _emptyArchetype;
        _commands = new CommandBuffer(this);
        _executor = executor;
    }

    /// <summary>The built-in deferred-mutation buffer for structural changes. See <see cref="CommandBuffer"/>.</summary>
    public CommandBuffer Commands => _commands;

    /// <summary>
    /// Creates an additional <see cref="CommandBuffer"/> bound to this world, independent of
    /// <see cref="Commands"/>. Each buffer is single-writer, so several concurrent sources can
    /// queue structural changes lock-free by using their own buffer, then applying them via
    /// <see cref="ApplyCommands(CommandBuffer)"/> in whatever order the caller chooses.
    /// </summary>
    public CommandBuffer CreateCommands() => new(this);

    /// <summary>
    /// Destroys an entity and all its components. Called only via <see cref="CommandBuffer"/>.
    /// Notifies observers before removing the entity. Unlike every other structural
    /// notification (which fires after the change lands), destruction leaves nothing
    /// queryable afterward, so "before" is the only point an observer can still read
    /// anything about the entity.
    /// </summary>
    internal void DestroyEntity(Entity entity)
    {
        RequireAlive(entity);
        NotifyEntityDestroyed(entity);
        CascadeRemoveRelations(entity);
        _entityTable.Destroy(entity.Id);
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

    /// <summary>The current tick, starting at 1. Every tracked write stamps the row it touches with this value.</summary>
    public int CurrentTick => _currentTick;

    /// <summary>Raised at the end of <see cref="AdvanceTick"/> with the new tick value, letting a per-tick background behavior (continuous persistence's capture step) hook in without the caller's tick loop needing to know about it.</summary>
    public event Action<int>? OnTickAdvanced;

    /// <summary>Advances to the next tick.</summary>
    public void AdvanceTick()
    {
        _currentTick++;
        foreach (var channel in _activeEventChannels) channel.Swap();
        OnTickAdvanced?.Invoke(_currentTick);
    }

    /// <summary>Runs one iteration of every registered system (see <c>WorldBuilder.AddSystemCore</c>/the generated <c>AddSystem&lt;T&gt;()</c>), staged by the static parallel schedule computed at <see cref="WorldBuilder.Build"/> time.</summary>
    public void Update(TimeSpan delta)
    {
        AdvanceTick();
        _totalElapsed += delta;
        _executor.RunStages(this, new Time(delta, _totalElapsed));
    }

    /// <summary>Runs <paramref name="system"/> once, outside the normal schedule (a harness/test convenience). Advances <see cref="CurrentTick"/> the same way <see cref="Update"/> does.</summary>
    public void RunOnce(EcsSystem system, TimeSpan delta)
    {
        AdvanceTick();
        _totalElapsed += delta;
        system.InvokeExecute(this, new Time(delta, _totalElapsed));
    }

    /// <summary>
    /// Registers one system against this already-running <see cref="World"/>, constructing
    /// it immediately (so <see cref="GetSystem{T}"/> reflects it right away) but deferring
    /// its stage placement to the next <see cref="Update"/> call — see
    /// <see cref="ISystemScheduler"/>. Not called directly by consumer code — the generator
    /// emits a strongly-typed <c>AddSystem&lt;T&gt;()</c> overload closing over this, the
    /// same way <see cref="WorldBuilder.AddSystemCore"/> does for the build-time path.
    /// Returns a chainable <see cref="SystemRegistration"/> for declaring ordering edges;
    /// <see cref="SystemRegistration.Build"/> is unavailable on the result (there's nothing
    /// to build — this <see cref="World"/> already exists and is already running).
    /// </summary>
    public SystemRegistration AddSystemCore(
        Type systemType,
        SystemAccess? access,
        Func<World, EcsSystem> construct,
        IReadOnlyList<Type> generatedBeforeTargets,
        IReadOnlyList<Type> generatedAfterTargets)
    {
        var entry = new SystemEntry { SystemType = systemType, Construct = construct, Access = access };
        entry.BeforeTargets.AddRange(generatedBeforeTargets);
        entry.AfterTargets.AddRange(generatedAfterTargets);
        return _executor.Register(entry, this);
    }

    /// <summary>
    /// Forces an immediate recompute if the schedule is currently dirty from a runtime
    /// <see cref="AddSystemCore"/>/<see cref="RemoveSystem(EcsSystem)"/> call — otherwise a
    /// no-op. <see cref="Update"/> already does this automatically at the start of every
    /// tick; call this directly right after a batch of runtime registrations if you want
    /// a bad edge (naming a type that never registered), a cycle, or an ambiguous target
    /// to throw immediately, at this call site, instead of waiting for the next <see cref="Update"/>.
    /// </summary>
    public void FlushSystemChanges() => _executor.Flush();

    /// <summary>The live instance registered for <typeparamref name="T"/>. Throws if none is registered — use <see cref="TryGetSystem{T}"/> if that's expected.</summary>
    public T GetSystem<T>() where T : EcsSystem =>
        _executor.Find(typeof(T)) as T ?? throw new InvalidOperationException($"No system of type {typeof(T)} is registered.");

    /// <summary>Same as <see cref="GetSystem{T}"/>, without throwing when nothing is registered.</summary>
    public bool TryGetSystem<T>(out T? system) where T : EcsSystem
    {
        system = _executor.Find(typeof(T)) as T;
        return system is not null;
    }

    /// <summary>Removes the registered <typeparamref name="T"/>, calling its <see cref="EcsSystem.OnDestroy"/> hook exactly once. Returns false if none was registered.</summary>
    public bool RemoveSystem<T>() where T : EcsSystem =>
        _executor.Find(typeof(T)) is EcsSystem system && RemoveSystem(system);

    /// <summary>Removes <paramref name="system"/>, calling its <see cref="EcsSystem.OnDestroy"/> hook exactly once. Returns false if it wasn't registered (already removed, or never was).</summary>
    public bool RemoveSystem(EcsSystem system)
    {
        if (!_executor.Remove(system)) return false;
        system.InvokeOnDestroy();
        return true;
    }

    /// <summary>Applies every command queued on <see cref="Commands"/>, in queued order, then clears the queue.</summary>
    public void ApplyCommands() => ApplyCommands(_commands);

    /// <summary>Applies every command queued on <paramref name="commands"/>, in queued order, then clears its queue. <paramref name="commands"/> may be <see cref="Commands"/> or any buffer from <see cref="CreateCommands"/>. Throws if it was created for a different <see cref="World"/>.</summary>
    public void ApplyCommands(CommandBuffer commands)
    {
        if (commands.World != this)
            throw new InvalidOperationException("This CommandBuffer was created for a different World.");

        commands.Apply();
        _entityTable.FlushReservations();
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
    internal void PlaceReservedEntityFromTemplate(Entity entity, Internal.ArchetypeSignature signature, TemplateComponentSetter[] setters)
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
    internal void PlaceReservedEntitiesFromTemplate(Entity[] entities, Internal.ArchetypeSignature signature, TemplateComponentSetter[] setters)
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

    /// <summary>Adds <typeparamref name="T"/> to <paramref name="entity"/> and returns a tracked mutable reference to it. Throws if the entity already has the component. Called only via <see cref="CommandBuffer"/>.</summary>
    internal ref T AddComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        return ref AddComponent<T>(entity, _entityTable[entity.Id]);
    }

    /// <summary>Same as <see cref="AddComponent{T}(Entity)"/>, for a caller that already resolved <paramref name="source"/> and shouldn't pay for a second entity-table read.</summary>
    internal ref T AddComponent<T>(Entity entity, EntityLocation source) where T : struct, IComponent
    {
        var typeIndex = TypeIndex<T>.Value;
        if (source.Archetype.Signature.Contains(typeIndex))
            throw new InvalidOperationException($"Entity {entity} already has component {typeof(T)}.");

        var target = MoveViaAddEdge(entity, source.Archetype, source.Row, typeIndex);

        var storage = target.Archetype.GetOrCreateStorage<T>();
        MarkDirtyIfTracked(storage, target.Row);
        NotifyComponentAdded(entity, typeIndex);
        return ref storage[target.Row];
    }

    /// <summary>
    /// Returns a tracked mutable reference to <paramref name="entity"/>'s <typeparamref name="T"/>.
    /// Throws if the entity doesn't have it. Do not hold the returned reference across a call to
    /// <see cref="ApplyCommands()"/>: a structural change applied afterward can silently
    /// invalidate it (a swap-remove can make it alias a different entity's data).
    /// </summary>
    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        return ref GetComponent<T>(entity, _entityTable[entity.Id]);
    }

    /// <summary>Same as <see cref="GetComponent{T}(Entity)"/>, for a caller that already resolved <paramref name="location"/> (avoids a second entity-table lookup).</summary>
    internal ref T GetComponent<T>(Entity entity, EntityLocation location) where T : struct, IComponent
    {
        if (!location.Archetype.Storages.TryGetValue(TypeIndex<T>.Value, out var storage))
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T)}.");

        var typed = (ComponentStorage<T>)storage;
        MarkDirtyIfTracked(typed, location.Row);
        return ref typed[location.Row];
    }

    /// <summary>A non-storable, world-scoped bound view over <paramref name="entity"/>. See <see cref="EntityView"/>.</summary>
    public EntityView this[Entity entity] => new(this, Commands, entity);

    /// <summary>
    /// Same tracked-ref contract as <see cref="GetComponent{T}(Entity)"/>, with <paramref name="found"/>
    /// instead of a throw. When <paramref name="found"/> is false, the returned reference must not
    /// be dereferenced.
    /// </summary>
    public ref T TryGetComponent<T>(Entity entity, out bool found) where T : struct, IComponent
    {
        RequireAlive(entity);
        var (archetype, row) = _entityTable[entity.Id];
        if (!archetype.Storages.TryGetValue(TypeIndex<T>.Value, out var storage))
        {
            found = false;
            return ref Unsafe.NullRef<T>();
        }

        found = true;
        var typed = (ComponentStorage<T>)storage;
        MarkDirtyIfTracked(typed, row);
        return ref typed[row];
    }

    /// <summary>True if <paramref name="entity"/> has a <typeparamref name="T"/> component.</summary>
    public bool HasComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        return _entityTable[entity.Id].Archetype.Signature.Contains(TypeIndex<T>.Value);
    }

    /// <summary>Removes the component at <paramref name="typeIndex"/> from <paramref name="entity"/>, if present. Called only via <see cref="CommandBuffer"/>, which already resolved the type index at its own call site: an archetype move only ever needs the index, not the type.</summary>
    internal void RemoveComponent(Entity entity, int typeIndex)
    {
        RequireAlive(entity);
        RemoveComponent(entity, _entityTable[entity.Id], typeIndex);
    }

    /// <summary>Same as <see cref="RemoveComponent(Entity, int)"/>, for a caller that already resolved <paramref name="source"/> (avoids a second entity-table lookup).</summary>
    internal void RemoveComponent(Entity entity, EntityLocation source, int typeIndex)
    {
        if (!source.Archetype.Signature.Contains(typeIndex)) return;

        MoveViaRemoveEdge(entity, source.Archetype, source.Row, typeIndex);
        NotifyComponentRemoved(entity, typeIndex);
    }

    /// <summary>Adds the tag at <paramref name="typeIndex"/> to <paramref name="entity"/>. Called only via <see cref="CommandBuffer"/>.</summary>
    internal void AddTag(Entity entity, int typeIndex)
    {
        RequireAlive(entity);
        AddTag(entity, _entityTable[entity.Id], typeIndex);
    }

    /// <summary>Same as <see cref="AddTag(Entity, int)"/>, for a caller that already resolved <paramref name="source"/> (avoids a second entity-table lookup).</summary>
    internal void AddTag(Entity entity, EntityLocation source, int typeIndex)
    {
        if (source.Archetype.Signature.Contains(typeIndex)) return;

        MoveViaAddEdge(entity, source.Archetype, source.Row, typeIndex);
        NotifyTagAdded(entity, typeIndex);
    }

    /// <summary>Removes the tag at <paramref name="typeIndex"/> from <paramref name="entity"/>, if present. Called only via <see cref="CommandBuffer"/>.</summary>
    internal void RemoveTag(Entity entity, int typeIndex)
    {
        RequireAlive(entity);
        RemoveTag(entity, _entityTable[entity.Id], typeIndex);
    }

    /// <summary>Same as <see cref="RemoveTag(Entity, int)"/>, for a caller that already resolved <paramref name="source"/> (avoids a second entity-table lookup).</summary>
    internal void RemoveTag(Entity entity, EntityLocation source, int typeIndex)
    {
        if (!source.Archetype.Signature.Contains(typeIndex)) return;

        MoveViaRemoveEdge(entity, source.Archetype, source.Row, typeIndex);
        NotifyTagRemoved(entity, typeIndex);
    }

    /// <summary>
    /// Returns a mutable reference to <paramref name="source"/>'s <see cref="RelationLinks{T}"/>,
    /// creating it (and moving <paramref name="source"/> onto the archetype that includes it) on
    /// first use. Called only via <see cref="CommandBuffer"/>'s relation ops and this class's own
    /// destroy-cascade path.
    /// </summary>
    internal ref RelationLinks<T> GetOrCreateRelationLinks<T>(Entity source, EntityLocation location) where T : struct, IRelation
    {
        var typeIndex = TypeIndex<RelationLinks<T>>.Value;
        ref var links = ref location.Archetype.Signature.Contains(typeIndex)
            ? ref GetComponent<RelationLinks<T>>(source, location)
            : ref AddComponent<RelationLinks<T>>(source, location);
        if (links.Targets is null) links = new RelationLinks<T>(new Dictionary<Entity, T>());
        return ref links;
    }

    /// <summary>Same as <see cref="GetOrCreateRelationLinks{T}"/>, for the reverse (<see cref="RelationBacklinks{T}"/>) side.</summary>
    internal ref RelationBacklinks<T> GetOrCreateRelationBacklinks<T>(Entity target, EntityLocation location) where T : struct, IRelation
    {
        var typeIndex = TypeIndex<RelationBacklinks<T>>.Value;
        ref var backlinks = ref location.Archetype.Signature.Contains(typeIndex)
            ? ref GetComponent<RelationBacklinks<T>>(target, location)
            : ref AddComponent<RelationBacklinks<T>>(target, location);
        if (backlinks.Sources is null) backlinks = new RelationBacklinks<T>(new HashSet<Entity>());
        return ref backlinks;
    }

    /// <summary>Removes <paramref name="target"/> from <paramref name="source"/>'s <see cref="RelationLinks{T}"/>, if present, removing the component entirely if that was its last edge. Resolves <paramref name="source"/>'s location itself, so it's always safe to call with a stale location.</summary>
    internal void RemoveRelationLink<T>(Entity source, Entity target) where T : struct, IRelation
    {
        if (!TryResolve(source, out var location)) return;
        var typeIndex = TypeIndex<RelationLinks<T>>.Value;
        if (!location.Archetype.Signature.Contains(typeIndex)) return;

        var links = GetComponent<RelationLinks<T>>(source, location);
        if (!links.Targets!.Remove(target)) return;
        if (links.Targets.Count == 0) RemoveComponent(source, location, typeIndex);
    }

    /// <summary>
    /// Same as <see cref="RemoveRelationLink{T}"/>, for the reverse (<see cref="RelationBacklinks{T}"/>)
    /// side. This is the single point that notifies <see cref="IStructuralChangeObserver.OnRelationUnlinked"/>
    /// for an edge removal, not <see cref="RemoveRelationLink{T}"/>: every caller that removes an edge
    /// calls this method somewhere in the process except <see cref="RelationBacklinks{T}"/>'s own
    /// cascade-remove (which notifies explicitly instead, since it deliberately skips this method; see
    /// its own doc).
    /// </summary>
    internal void RemoveRelationBacklink<T>(Entity target, Entity source) where T : struct, IRelation
    {
        if (!TryResolve(target, out var location)) return;
        var typeIndex = TypeIndex<RelationBacklinks<T>>.Value;
        if (!location.Archetype.Signature.Contains(typeIndex)) return;

        var backlinks = GetComponent<RelationBacklinks<T>>(target, location);
        if (!backlinks.Sources!.Remove(source)) return;
        NotifyRelationUnlinked(source, target, TypeIndex<T>.Value);
        if (backlinks.Sources.Count == 0) RemoveComponent(target, location, typeIndex);
    }

    /// <summary>
    /// For an <see cref="IExclusiveRelation"/> type, removes every existing target other than
    /// <paramref name="target"/> before <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/>'s
    /// apply-time op adds the new edge. Mutates <see cref="RelationLinks{T}.Targets"/> directly
    /// rather than going through <see cref="RemoveRelationLink{T}"/>, to avoid an archetype move
    /// followed immediately by another one re-adding it.
    /// </summary>
    internal void ReplaceExclusiveRelationTarget<T>(Entity source, Entity target) where T : struct, IRelation
    {
        if (!TryResolve(source, out var location)) return;
        var typeIndex = TypeIndex<RelationLinks<T>>.Value;
        if (!location.Archetype.Signature.Contains(typeIndex)) return;

        var targets = GetComponent<RelationLinks<T>>(source, location).Targets!;
        if (targets.Count == 0) return;

        if (targets.Count == 1)
        {
            foreach (var existingTarget in targets.Keys)
            {
                if (existingTarget == target) return; // re-adding the same target: nothing to replace
                RemoveRelationBacklink<T>(existingTarget, source);
                targets.Remove(existingTarget);
                return;
            }
        }

        // More than one existing target shouldn't happen for a type that's always been exclusive,
        // but could if T was only just marked IExclusiveRelation after edges already existed.
        // ToArray snapshots before mutating the same dictionary.
        foreach (var existingTarget in targets.Keys.ToArray())
        {
            if (existingTarget == target) continue;
            RemoveRelationBacklink<T>(existingTarget, source);
            targets.Remove(existingTarget);
        }
    }

    /// <summary>Shared by every add path: looks up (or creates and caches) the archetype-add edge for <paramref name="typeIndex"/> and moves the entity onto it.</summary>
    private EntityLocation MoveViaAddEdge(Entity entity, Archetype source, int sourceRow, int typeIndex)
    {
        if (!source.TryGetAddEdge(typeIndex, out var target))
        {
            target = GetOrCreateArchetype(source.Signature.With(typeIndex), source);
            source.SetAddEdge(typeIndex, target);
        }

        var targetRow = MoveEntity(entity, source, sourceRow, target);
        return new EntityLocation(target, targetRow);
    }

    /// <summary>Shared by every remove path: looks up (or creates and caches) the archetype-remove edge for <paramref name="typeIndex"/> and moves the entity onto it.</summary>
    private EntityLocation MoveViaRemoveEdge(Entity entity, Archetype source, int sourceRow, int typeIndex)
    {
        if (!source.TryGetRemoveEdge(typeIndex, out var target))
        {
            target = GetOrCreateArchetype(source.Signature.Without(typeIndex), source);
            source.SetRemoveEdge(typeIndex, target);
        }

        var targetRow = MoveEntity(entity, source, sourceRow, target);
        return new EntityLocation(target, targetRow);
    }

    /// <summary>True if <paramref name="entity"/> has tag <typeparamref name="T"/>.</summary>
    public bool HasTag<T>(Entity entity) where T : struct, ITag
    {
        RequireAlive(entity);
        return _entityTable[entity.Id].Archetype.Signature.Contains(TypeIndex<T>.Value);
    }

    /// <summary>True if <paramref name="source"/> has a <typeparamref name="T"/> edge to <paramref name="target"/>.</summary>
    public bool HasRelation<T>(Entity source, Entity target) where T : struct, IRelation
    {
        RequireAlive(source);
        var (archetype, row) = _entityTable[source.Id];
        if (!archetype.Storages.TryGetValue(TypeIndex<RelationLinks<T>>.Value, out var storage)) return false;
        return ((ComponentStorage<RelationLinks<T>>)storage)[row].Targets!.ContainsKey(target);
    }

    /// <summary>
    /// Tracked mutable reference to the payload of <paramref name="source"/>'s <typeparamref name="T"/>
    /// edge to <paramref name="target"/>, with <paramref name="found"/> reporting whether it exists.
    /// Same ref-lifetime caveat as <see cref="GetComponent{T}(Entity)"/>: a later <c>AddRelation</c>
    /// for a different target of the same source/relation can silently detach this reference.
    /// </summary>
    public ref T TryGetRelation<T>(Entity source, Entity target, out bool found) where T : struct, IRelation
    {
        RequireAlive(source);
        var (archetype, row) = _entityTable[source.Id];
        if (!archetype.Storages.TryGetValue(TypeIndex<RelationLinks<T>>.Value, out var storage))
        {
            found = false;
            return ref Unsafe.NullRef<T>();
        }

        var typed = (ComponentStorage<RelationLinks<T>>)storage;
        ref var edgeValue = ref CollectionsMarshal.GetValueRefOrNullRef(typed[row].Targets!, target);
        found = !Unsafe.IsNullRef(ref edgeValue);
        if (found) MarkDirtyIfTracked(typed, row);
        return ref edgeValue;
    }

    /// <summary>
    /// Tracked mutable reference to the payload of <paramref name="source"/>'s existing
    /// <typeparamref name="T"/> edge to <paramref name="target"/>. Throws if no such edge exists.
    /// Never creates or removes an edge. Use <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/>/
    /// <see cref="CommandBuffer.RemoveRelation{T}(Entity, Entity)"/> for that. Same ref-lifetime
    /// caveat as <see cref="TryGetRelation{T}"/>.
    /// </summary>
    public ref T GetRelation<T>(Entity source, Entity target) where T : struct, IRelation
    {
        RequireAlive(source);
        var (archetype, row) = _entityTable[source.Id];
        if (!archetype.Storages.TryGetValue(TypeIndex<RelationLinks<T>>.Value, out var storage))
            throw new InvalidOperationException($"Entity {source} has no {typeof(T)} edges.");

        var typed = (ComponentStorage<RelationLinks<T>>)storage;
        ref var edgeValue = ref CollectionsMarshal.GetValueRefOrNullRef(typed[row].Targets!, target);
        if (Unsafe.IsNullRef(ref edgeValue))
            throw new InvalidOperationException($"Entity {source} has no {typeof(T)} edge to {target}.");

        MarkDirtyIfTracked(typed, row);
        return ref edgeValue;
    }

    private static class EmptyRelation<T>
    {
        internal static readonly IReadOnlyDictionary<Entity, T> Targets = new Dictionary<Entity, T>();
        internal static readonly IReadOnlyCollection<Entity> Entities = Array.Empty<Entity>();
    }

    /// <summary>Every target <paramref name="source"/> has a <typeparamref name="T"/> edge to, and each edge's payload. Empty, not throwing, if none.</summary>
    public IReadOnlyDictionary<Entity, T> Targets<T>(Entity source) where T : struct, IRelation
    {
        RequireAlive(source);
        var (archetype, row) = _entityTable[source.Id];
        return archetype.Storages.TryGetValue(TypeIndex<RelationLinks<T>>.Value, out var storage)
            ? ((ComponentStorage<RelationLinks<T>>)storage)[row].Values
            : EmptyRelation<T>.Targets;
    }

    /// <summary>Every source entity with a <typeparamref name="T"/> edge to <paramref name="target"/>. Empty, not throwing, if none.</summary>
    public IReadOnlyCollection<Entity> Sources<T>(Entity target) where T : struct, IRelation
    {
        RequireAlive(target);
        var (archetype, row) = _entityTable[target.Id];
        return archetype.Storages.TryGetValue(TypeIndex<RelationBacklinks<T>>.Value, out var storage)
            ? ((ComponentStorage<RelationBacklinks<T>>)storage)[row].Values
            : EmptyRelation<T>.Entities;
    }

    /// <summary>Yields one <see cref="EncodedComponent"/> per (entity, registered component type) pair for every live entity. Unregistered types and tags are skipped. A full-world walk, for a save/checkpoint, not a per-tick path.</summary>
    public IEnumerable<EncodedComponent> EnumerateAll(ComponentCodecRegistry registry)
    {
        foreach (var archetype in _archetypes.Values)
        {
            if (archetype.Count == 0) continue;

            foreach (var (typeIndex, storage) in archetype.Storages)
            {
                if (!registry.TryGetByTypeIndex(typeIndex, out var registered)) continue;

                for (var row = 0; row < archetype.Count; row++)
                    yield return new EncodedComponent(archetype.Entities[row], registered.Discriminator, registered.SchemaHash, registered.EncodeRow(storage.RawItems, row));
            }
        }
    }

    /// <summary>
    /// Every live relation edge whose payload type is registered in <paramref name="registry"/>
    /// via <see cref="ComponentCodecRegistry.RegisterRelation{T}"/>, one <see cref="EncodedRelation"/>
    /// per edge. Mirrors <see cref="EnumerateAll"/>, walking <see cref="RelationLinks{T}"/>
    /// storages instead of ordinary component storages. <see cref="RelationBacklinks{T}"/>
    /// is never walked here: replaying an edge through
    /// <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/> regenerates it as a
    /// side effect, same as at runtime.
    /// </summary>
    public IEnumerable<EncodedRelation> EnumerateRelations(ComponentCodecRegistry registry)
    {
        foreach (var archetype in _archetypes.Values)
        {
            if (archetype.Count == 0) continue;

            foreach (var (typeIndex, storage) in archetype.Storages)
            {
                if (!registry.TryGetRelationByLinksTypeIndex(typeIndex, out var registered)) continue;

                for (var row = 0; row < archetype.Count; row++)
                {
                    var source = archetype.Entities[row];
                    foreach (var (target, payload) in registered.EncodeRow(storage.RawItems, row))
                        yield return new EncodedRelation(source, target, registered.Discriminator, registered.SchemaHash, payload);
                }
            }
        }
    }

    /// <summary>Hot-path query: invokes <paramref name="action"/> once per matching archetype chunk with a <typeparamref name="TAccess0"/> accessor.</summary>
    public void Query<TAccess0>(ChunkAction<TAccess0> action) where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
    {
        foreach (var chunk in Internal.ChunkQuery<TAccess0>.Value.Resolve(this))
            action(chunk.Access<TAccess0>());
    }

    /// <summary>Two-component overload, using <see cref="ChunkAction{TAccess0, TAccess1}"/>.</summary>
    public void Query<TAccess0, TAccess1>(ChunkAction<TAccess0, TAccess1> action)
        where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
        where TAccess1 : struct, IComponentAccessor<TAccess1>, allows ref struct
    {
        foreach (var chunk in Internal.ChunkQuery<TAccess0, TAccess1>.Value.Resolve(this))
            action(chunk.Access<TAccess0>(), chunk.Access<TAccess1>());
    }

    // World.Query<TAccess0>/Query<TAccess0,TAccess1> above are deliberately hand-written and
    // capped at arity 2: the zero-codegen chunk-callback tier, usable with no generator setup,
    // unlike the fluent Query<TShape> chain (Query.cs). For 3+ components, use the fluent chain.

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

    /// <summary>Only copies a storage when <paramref name="signature"/> still contains its type, so a just-removed component's storage is naturally excluded. Each clone is sized to the new archetype's capacity directly, matching the invariant <see cref="Archetype.EnsureCapacity"/> relies on.</summary>
    private Archetype GetOrCreateArchetype(ArchetypeSignature signature, Archetype templateSource)
    {
        if (_archetypes.TryGetValue(signature, out var existing)) return existing;

        var created = CreateArchetype(signature);
        foreach (var (typeIndex, sourceStorage) in templateSource.Storages)
        {
            if (signature.Contains(typeIndex))
                created.Storages[typeIndex] = sourceStorage.CreateEmpty(created.Entities.Length);
        }

        return created;
    }

    /// <summary>Registers a brand-new, storage-less archetype and invalidates every archetype-set cache. Callers populate the returned archetype's storages themselves.</summary>
    private Archetype CreateArchetype(ArchetypeSignature signature)
    {
        var created = new Archetype(signature, _archetypeCapacity);
        _archetypes[signature] = created;
        _queryCache.Clear();
        _filteredQueryCache.Clear();
        return created;
    }

    /// <summary>Total live entities across every archetype: O(archetype count), not O(entity count). A cheap, deliberately coarse size proxy the scheduler uses to decide whether a stage is worth dispatching to the thread pool.</summary>
    internal int TotalEntityCount => _archetypes.Values.Sum(a => a.Count);

    /// <summary>Every archetype whose signature contains all of <paramref name="required"/>'s bits, cached per required set and invalidated whenever a new archetype is created.</summary>
    internal Archetype[] GetMatchingArchetypes(ArchetypeSignature required)
    {
        if (_queryCache.TryGetValue(required, out var cached)) return cached;

        var matches = new List<Archetype>();
        foreach (var archetype in _archetypes.Values)
        {
            if (required.IsSubsetOf(archetype.Signature))
                matches.Add(archetype);
        }

        var result = matches.ToArray();
        _queryCache[required] = result;
        return result;
    }

    /// <summary>Same as <see cref="GetMatchingArchetypes(ArchetypeSignature)"/>, plus <paramref name="filter"/>'s Without/Any checks. A separate cache so callers that never filter (chunk queries, <see cref="ReadChanges{T}"/>) don't pay for an always-empty cache key.</summary>
    internal Archetype[] GetMatchingArchetypes(ArchetypeSignature required, ArchetypeFilter filter)
    {
        var key = (required, filter);
        if (_filteredQueryCache.TryGetValue(key, out var cached)) return cached;

        var matches = new List<Archetype>();
        foreach (var archetype in _archetypes.Values)
        {
            if (required.IsSubsetOf(archetype.Signature) && filter.Matches(archetype.Signature))
                matches.Add(archetype);
        }

        var result = matches.ToArray();
        _filteredQueryCache[key] = result;
        return result;
    }

    /// <summary>Moves an entity from <paramref name="source"/> to <paramref name="target"/>, copying every component <paramref name="target"/> also has.</summary>
    private int MoveEntity(Entity entity, Archetype source, int sourceRow, Archetype target)
    {
        var targetRow = target.AddRow(entity);

        foreach (var (typeIndex, sourceStorage) in source.Storages)
        {
            if (target.Storages.TryGetValue(typeIndex, out var targetStorage))
                sourceStorage.CopyRowTo(sourceRow, targetStorage, targetRow);
        }

        var moved = source.RemoveRow(sourceRow);
        if (!moved.IsNull)
            _entityTable[moved.Id] = new EntityLocation(source, sourceRow);

        _entityTable[entity.Id] = new EntityLocation(target, targetRow);
        return targetRow;
    }
}
