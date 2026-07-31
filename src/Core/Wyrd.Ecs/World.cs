using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// The concrete, real archetype-storage implementation of <see cref="IWorld"/>. See the
/// design's Core engine section — entities with identical component/tag sets share one
/// <see cref="Archetype"/>; adding/removing a component or tag moves the entity between
/// archetypes.
/// </summary>
public sealed partial class World : IWorld
{
    /// <summary>
    /// Default floor every archetype's dense arrays start at and never shrink below,
    /// when a <see cref="World"/> is constructed without going through
    /// <see cref="WorldBuilder.WithArchetypeCapacity"/>. The right number depends on a
    /// game's actual entity/archetype-count distribution, so this is a moderate,
    /// workload-agnostic default: big enough to skip several of the earliest doubling
    /// steps for a typical archetype, without assuming the large-few-archetypes shape a
    /// much bigger floor would.
    /// </summary>
    internal const int DefaultArchetypeCapacity = 64;

    private readonly Dictionary<ArchetypeSignature, Archetype> _archetypes = new();
    private readonly Dictionary<ArchetypeSignature, Archetype[]> _queryCache = new();
    private readonly Dictionary<(ArchetypeSignature Required, ArchetypeFilter Filter), Archetype[]> _filteredQueryCache = new();
    private TrackingState _tracking = new();
    private readonly Archetype _emptyArchetype;
    private readonly int _archetypeCapacity;
    private readonly CommandBuffer _commands;

    private EntityTable _entityTable = new();
    private int _currentTick = 1;

    private readonly ScheduledExecutor _executor;
    private TimeSpan _totalElapsed;

    /// <summary>Creates a new, empty world with <see cref="DefaultArchetypeCapacity"/>. Use <see cref="WorldBuilder"/> to configure it.</summary>
    public World() : this(DefaultArchetypeCapacity, new ScheduledExecutor([], 1000)) { }

    internal World(int archetypeCapacity, ScheduledExecutor executor)
    {
        _archetypeCapacity = archetypeCapacity;
        _emptyArchetype = new Archetype(ArchetypeSignature.Empty, archetypeCapacity);
        _archetypes[ArchetypeSignature.Empty] = _emptyArchetype;
        _commands = new CommandBuffer(this);
        _executor = executor;
    }

    /// <inheritdoc/>
    public CommandBuffer Commands => _commands;

    /// <inheritdoc/>
    public CommandBuffer CreateCommands() => new(this);

    private readonly List<IStructuralChangeObserver> _structuralObservers = new();

    /// <inheritdoc/>
    public IDisposable ObserveStructuralChanges(IStructuralChangeObserver observer)
    {
        _structuralObservers.Add(observer);
        return new StructuralObserverHandle(this, observer);
    }

    private void UnobserveStructuralChanges(IStructuralChangeObserver observer) => _structuralObservers.Remove(observer);

    private void NotifyEntityCreated(Entity entity)
    {
        foreach (var observer in _structuralObservers)
            observer.OnEntityCreated(entity);
    }

    private void NotifyEntityDestroyed(Entity entity)
    {
        foreach (var observer in _structuralObservers)
            observer.OnEntityDestroyed(entity);
    }

    private void NotifyComponentAdded(Entity entity, int typeIndex)
    {
        foreach (var observer in _structuralObservers)
            observer.OnComponentAdded(entity, typeIndex);
    }

    private void NotifyComponentRemoved(Entity entity, int typeIndex)
    {
        foreach (var observer in _structuralObservers)
            observer.OnComponentRemoved(entity, typeIndex);
    }

    private void NotifyTagAdded(Entity entity, int typeIndex)
    {
        foreach (var observer in _structuralObservers)
            observer.OnTagAdded(entity, typeIndex);
    }

    private void NotifyTagRemoved(Entity entity, int typeIndex)
    {
        foreach (var observer in _structuralObservers)
            observer.OnTagRemoved(entity, typeIndex);
    }

    private sealed class StructuralObserverHandle : IDisposable
    {
        private readonly World _world;
        private readonly IStructuralChangeObserver _observer;
        private bool _disposed;

        internal StructuralObserverHandle(World world, IStructuralChangeObserver observer)
        {
            _world = world;
            _observer = observer;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _world.UnobserveStructuralChanges(_observer);
        }
    }

    /// <summary>
    /// Destroys an entity and all of its components. Called only via <see cref="CommandBuffer"/>.
    /// Notifies observers before the entity table retires the id, not after — unlike
    /// every other structural notification, which fires once the change has already
    /// landed, destruction leaves nothing queryable afterward, so this is the one
    /// callback where "before" is the only point an observer can still read anything
    /// about the entity (its permanent id, whether it's alive, its components) at all.
    /// </summary>
    internal void DestroyEntity(Entity entity)
    {
        RequireAlive(entity);
        NotifyEntityDestroyed(entity);
        CascadeRemoveRelations(entity);
        _entityTable.Destroy(entity.Id);
    }

    /// <summary>
    /// Cleans up every relation edge touching <paramref name="entity"/>, in both
    /// directions, before its row is removed. Snapshots which of its current component
    /// type indices are relation storages once upfront, but re-resolves
    /// <paramref name="entity"/>'s location fresh before processing each one — a
    /// self-relation (<c>source == target == entity</c>) can have one relation type's
    /// cascade step remove a *different* relation component of <paramref name="entity"/>'s
    /// own as a side effect (its backlink set emptying out), which would move
    /// <paramref name="entity"/> to a different archetype/row and invalidate any location
    /// captured before that happened. The <c>Contains</c> check below skips a type index
    /// already cleaned up that way, rather than re-processing or reading stale storage.
    /// </summary>
    private void CascadeRemoveRelations(Entity entity)
    {
        var initialLocation = _entityTable[entity.Id];

        // Lazily allocated: stays null (zero allocation) for the common case of an entity
        // with no relation components at all -- this runs on every single entity destroy,
        // not just ones involving relations, so it must not cost anything for the ones that
        // don't.
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
            if (!current.Archetype.Signature.Contains(typeIndex)) continue;

            current.Archetype.Storages.TryGetValue(typeIndex, out var storage);
            handler(this, entity, storage, current.Row);
        }
    }

    /// <inheritdoc/>
    public bool IsAlive(Entity entity) => _entityTable.IsAlive(entity.Id, entity.Generation);

    /// <inheritdoc/>
    public EntityId GetPermanentId(Entity entity)
    {
        RequireAlive(entity);
        return _entityTable.PermanentId(entity.Id);
    }

    /// <inheritdoc/>
    public int CurrentTick => _currentTick;

    /// <summary>
    /// Raised at the end of <see cref="AdvanceTick"/> with the new tick value — the
    /// extensibility hook a per-tick background behavior (continuous persistence's
    /// capture step, for one) subscribes to, so its one-time setup is the only code a
    /// consumer needs beyond their existing tick loop. A caller with no subscribers
    /// pays nothing extra.
    /// </summary>
    public event Action<int>? OnTickAdvanced;

    /// <inheritdoc/>
    public void AdvanceTick()
    {
        _currentTick++;
        OnTickAdvanced?.Invoke(_currentTick);
    }

    /// <summary>
    /// Runs one iteration of every system registered via <see cref="WorldBuilder.WithSystems"/>,
    /// staged by the static parallel schedule computed once at <see cref="WorldBuilder.Build"/>
    /// time. Advances <see cref="CurrentTick"/> and accumulates <paramref name="delta"/> into
    /// a running total before handing both down as a single <see cref="Time"/> value — the
    /// only tick concept a system ever sees.
    /// </summary>
    public void Tick(TimeSpan delta)
    {
        AdvanceTick();
        _totalElapsed += delta;
        _executor.RunTick(this, new Time(delta, _totalElapsed));
    }

    /// <summary>
    /// Runs <paramref name="system"/> once, without going through the scheduled stages —
    /// a harness/test convenience, or for a system deliberately run outside the normal
    /// schedule. Advances <see cref="CurrentTick"/> and the running elapsed-time total the
    /// same way <see cref="Tick"/> does, so the two stay consistent regardless of which one
    /// a caller mixes in.
    /// </summary>
    public void RunOnce(EcsSystem system, TimeSpan delta)
    {
        AdvanceTick();
        _totalElapsed += delta;
        system.InvokeExecute(this, new Time(delta, _totalElapsed));
    }

    /// <inheritdoc/>
    public void ApplyCommands() => ApplyCommands(_commands);

    /// <inheritdoc/>
    public void ApplyCommands(CommandBuffer commands)
    {
        if (commands.World != this)
            throw new InvalidOperationException("This CommandBuffer was created for a different World.");

        commands.Apply();
        _entityTable.FlushReservations();
    }

    /// <summary>Reserves a fresh entity id without placing it — see <see cref="Internal.EntityTable.Reserve"/>. Used only by <see cref="CommandBuffer.CreateEntity()"/>.</summary>
    internal Entity ReserveEntity() => _entityTable.Reserve();

    /// <summary>Bulk counterpart to <see cref="ReserveEntity"/> — see <see cref="Internal.EntityTable.ReserveRange"/>. Used by <see cref="CommandBuffer.CreateEntity(int)"/> and its component-carrying siblings.</summary>
    internal void ReserveEntityRange(Span<Entity> destination) => _entityTable.ReserveRange(destination);

    /// <summary>Places a previously-reserved entity into the empty archetype, making it alive, and notifies observers. Used only by <see cref="CommandBuffer.CreateEntity()"/>'s queued placement.</summary>
    internal void PlaceReservedEntity(Entity entity)
    {
        _entityTable.Place(entity, _emptyArchetype);
        NotifyEntityCreated(entity);
    }

    /// <summary>
    /// Bulk counterpart to <see cref="PlaceReservedEntity(Entity)"/>: places every entity
    /// in <paramref name="entities"/> into the empty archetype in one
    /// <see cref="Internal.Archetype.AddRows"/> call. Used only by
    /// <see cref="CommandBuffer.CreateEntity(int)"/>'s queued placement.
    /// </summary>
    internal void PlaceReservedEntities(Entity[] entities)
    {
        var startRow = _emptyArchetype.AddRows(entities);
        _entityTable.PlaceBatch(entities, _emptyArchetype, startRow);
        foreach (var entity in entities) NotifyEntityCreated(entity);
    }

    /// <summary>
    /// Adds <typeparamref name="T"/> to <paramref name="entity"/> and returns a
    /// tracked mutable reference to it. Throws if the entity already has the
    /// component. Called only via <see cref="CommandBuffer"/>.
    /// </summary>
    internal ref T AddComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        return ref AddComponent<T>(entity, _entityTable[entity.Id]);
    }

    /// <summary>
    /// Same as <see cref="AddComponent{T}(Entity)"/>, for a caller (<see cref="CommandBuffer"/>'s
    /// apply-time delegates) that already resolved <paramref name="source"/> via
    /// <see cref="TryResolve"/> and shouldn't pay for a second entity-table read to get it again.
    /// </summary>
    internal ref T AddComponent<T>(Entity entity, EntityLocation source) where T : struct, IComponent
    {
        var typeIndex = TypeIndex<T>.Value;
        if (source.Archetype.Signature.Contains(typeIndex))
            throw new InvalidOperationException($"Entity {entity} already has component {typeof(T)}.");

        var target = MoveViaAddEdge(entity, source.Archetype, source.Row, typeIndex);

        var storage = target.Archetype.GetOrCreateStorage<T>();
        if (IsTracked(typeIndex))
            storage.MarkDirty(target.Row, _currentTick);
        NotifyComponentAdded(entity, typeIndex);
        return ref storage[target.Row];
    }

    /// <inheritdoc/>
    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        return ref GetComponent<T>(entity, _entityTable[entity.Id]);
    }

    /// <summary>Same as <see cref="GetComponent{T}(Entity)"/>, for an already-resolved <paramref name="location"/> — see <see cref="AddComponent{T}(Entity, EntityLocation)"/>'s docs for why.</summary>
    internal ref T GetComponent<T>(Entity entity, EntityLocation location) where T : struct, IComponent
    {
        if (!location.Archetype.Storages.TryGetValue(TypeIndex<T>.Value, out var storage))
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T)}.");

        var typed = (ComponentStorage<T>)storage;
        if (IsTracked(TypeIndex<T>.Value))
            typed.MarkDirty(location.Row, _currentTick);
        return ref typed[location.Row];
    }

    /// <inheritdoc/>
    public EntityView this[Entity entity] => new(this, entity);

    /// <inheritdoc/>
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
        if (IsTracked(TypeIndex<T>.Value))
            typed.MarkDirty(row, _currentTick);
        return ref typed[row];
    }

    /// <inheritdoc/>
    public bool HasComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        return _entityTable[entity.Id].Archetype.Signature.Contains(TypeIndex<T>.Value);
    }

    /// <summary>
    /// Removes the component at <paramref name="typeIndex"/> from <paramref name="entity"/>,
    /// if present. Called only via <see cref="CommandBuffer"/>, which already has the caller's
    /// compile-time component type at its own call site and resolves it to a
    /// <see cref="Internal.TypeIndex{T}"/> there — the move itself (like every archetype
    /// transition) only ever needs the type index, never the type, so there is nothing
    /// for this method itself to be generic over.
    /// </summary>
    internal void RemoveComponent(Entity entity, int typeIndex)
    {
        RequireAlive(entity);
        RemoveComponent(entity, _entityTable[entity.Id], typeIndex);
    }

    /// <summary>Same as <see cref="RemoveComponent(Entity, int)"/>, for an already-resolved <paramref name="source"/> — see <see cref="AddComponent{T}(Entity, EntityLocation)"/>'s docs for why.</summary>
    internal void RemoveComponent(Entity entity, EntityLocation source, int typeIndex)
    {
        if (!source.Archetype.Signature.Contains(typeIndex)) return;

        MoveViaRemoveEdge(entity, source.Archetype, source.Row, typeIndex);
        NotifyComponentRemoved(entity, typeIndex);
    }

    /// <summary>Adds the tag at <paramref name="typeIndex"/> to <paramref name="entity"/>. Called only via <see cref="CommandBuffer"/> — see <see cref="RemoveComponent(Entity, int)"/> for why this takes a type index, not a type parameter.</summary>
    internal void AddTag(Entity entity, int typeIndex)
    {
        RequireAlive(entity);
        AddTag(entity, _entityTable[entity.Id], typeIndex);
    }

    /// <summary>Same as <see cref="AddTag(Entity, int)"/>, for an already-resolved <paramref name="source"/> — see <see cref="AddComponent{T}(Entity, EntityLocation)"/>'s docs for why.</summary>
    internal void AddTag(Entity entity, EntityLocation source, int typeIndex)
    {
        if (source.Archetype.Signature.Contains(typeIndex)) return;

        MoveViaAddEdge(entity, source.Archetype, source.Row, typeIndex);
        NotifyTagAdded(entity, typeIndex);
    }

    /// <summary>Removes the tag at <paramref name="typeIndex"/> from <paramref name="entity"/>, if present. Called only via <see cref="CommandBuffer"/> — see <see cref="RemoveComponent(Entity, int)"/> for why this takes a type index, not a type parameter.</summary>
    internal void RemoveTag(Entity entity, int typeIndex)
    {
        RequireAlive(entity);
        RemoveTag(entity, _entityTable[entity.Id], typeIndex);
    }

    /// <summary>Same as <see cref="RemoveTag(Entity, int)"/>, for an already-resolved <paramref name="source"/> — see <see cref="AddComponent{T}(Entity, EntityLocation)"/>'s docs for why.</summary>
    internal void RemoveTag(Entity entity, EntityLocation source, int typeIndex)
    {
        if (!source.Archetype.Signature.Contains(typeIndex)) return;

        MoveViaRemoveEdge(entity, source.Archetype, source.Row, typeIndex);
        NotifyTagRemoved(entity, typeIndex);
    }

    /// <summary>
    /// Returns a mutable reference to <paramref name="source"/>'s <see cref="RelationLinks{T}"/>
    /// for relation type <typeparamref name="T"/>, creating it (and moving <paramref name="source"/>
    /// onto the archetype that includes it) if this is its first edge of this relation type.
    /// The backing dictionary is always non-null on return — a freshly created component's
    /// default value has a null one, initialized here in the same call before anything else
    /// can observe it. Called only via <see cref="CommandBuffer"/>'s relation ops and this
    /// class's own <see cref="RemoveRelationLink{T}"/>/destroy-cascade path.
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

    /// <summary>
    /// Removes <paramref name="target"/> from <paramref name="source"/>'s <see cref="RelationLinks{T}"/>,
    /// if present, removing the component entirely if that was its last edge — the same
    /// archetype-move-only-when-the-set-empties rule <see cref="GetOrCreateRelationLinks{T}"/>
    /// has for adding. A no-op if <paramref name="source"/> is dead, doesn't have any
    /// <typeparamref name="T"/> edges, or never had this specific one. Resolves
    /// <paramref name="source"/>'s location itself rather than taking one, so it's always
    /// safe to call with a location resolved before some other write that might have moved
    /// it (see <see cref="CommandBuffer.RemoveRelation{T}"/>'s self-relation handling).
    /// </summary>
    internal void RemoveRelationLink<T>(Entity source, Entity target) where T : struct, IRelation
    {
        if (!TryResolve(source, out var location)) return;
        var typeIndex = TypeIndex<RelationLinks<T>>.Value;
        if (!location.Archetype.Signature.Contains(typeIndex)) return;

        var links = GetComponent<RelationLinks<T>>(source, location);
        if (!links.Targets!.Remove(target)) return;
        if (links.Targets.Count == 0) RemoveComponent(source, location, typeIndex);
    }

    /// <summary>Same as <see cref="RemoveRelationLink{T}"/>, for the reverse (<see cref="RelationBacklinks{T}"/>) side.</summary>
    internal void RemoveRelationBacklink<T>(Entity target, Entity source) where T : struct, IRelation
    {
        if (!TryResolve(target, out var location)) return;
        var typeIndex = TypeIndex<RelationBacklinks<T>>.Value;
        if (!location.Archetype.Signature.Contains(typeIndex)) return;

        var backlinks = GetComponent<RelationBacklinks<T>>(target, location);
        if (!backlinks.Sources!.Remove(source)) return;
        if (backlinks.Sources.Count == 0) RemoveComponent(target, location, typeIndex);
    }

    /// <summary>
    /// For an <see cref="IExclusiveRelation"/> type, removes every existing
    /// <typeparamref name="T"/> target from <paramref name="source"/> other than
    /// <paramref name="target"/> itself (a no-op re-add), before
    /// <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/>'s apply-time op adds
    /// the new edge — see <see cref="IExclusiveRelation"/>'s own doc.
    ///
    /// <para>
    /// The common case for an exclusive relation is exactly one existing target, handled
    /// by mutating <see cref="RelationLinks{T}.Targets"/> directly rather than going
    /// through <see cref="RemoveRelationLink{T}"/> — that would remove
    /// <see cref="RelationLinks{T}"/> entirely once its dictionary empties (an archetype
    /// move), immediately followed by <see cref="GetOrCreateRelationLinks{T}"/> re-adding
    /// it (a second one) back in the caller. Holding the plain <c>Dictionary&lt;Entity,T&gt;</c>
    /// reference (not a <c>ref RelationLinks{T}</c>) across
    /// <see cref="RemoveRelationBacklink{T}"/> is safe regardless of any archetype move
    /// that call triggers — a struct value being relocated between archetypes copies the
    /// reference field, never the dictionary object it points to, so the object identity
    /// this method already captured stays valid even if <paramref name="source"/> is its
    /// own existing target and removing that backlink moves it.
    /// </para>
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

        // General path: more than one existing target -- shouldn't happen for a type that's
        // always been exclusive, but could if T was only just marked IExclusiveRelation
        // after edges already existed. ToArray snapshots before mutating the same dictionary.
        foreach (var existingTarget in targets.Keys.ToArray())
        {
            if (existingTarget == target) continue;
            RemoveRelationBacklink<T>(existingTarget, source);
            targets.Remove(existingTarget);
        }
    }

    /// <summary>
    /// Shared by every add path (<see cref="AddComponent{T}(Entity)"/>, <see cref="AddTag(Entity, int)"/>):
    /// looks up (or creates and caches) the archetype-add edge for <paramref name="typeIndex"/>
    /// and moves the entity onto it.
    /// </summary>
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

    /// <summary>
    /// Shared by every remove path (<see cref="RemoveComponent(Entity, int)"/>, <see cref="RemoveTag(Entity, int)"/>):
    /// looks up (or creates and caches) the archetype-remove edge for <paramref name="typeIndex"/>
    /// and moves the entity onto it.
    /// </summary>
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

    /// <inheritdoc/>
    public bool HasTag<T>(Entity entity) where T : struct, ITag
    {
        RequireAlive(entity);
        return _entityTable[entity.Id].Archetype.Signature.Contains(TypeIndex<T>.Value);
    }

    /// <inheritdoc/>
    public bool HasRelation<T>(Entity source, Entity target) where T : struct, IRelation
    {
        RequireAlive(source);
        var (archetype, row) = _entityTable[source.Id];
        if (!archetype.Storages.TryGetValue(TypeIndex<RelationLinks<T>>.Value, out var storage)) return false;
        return ((ComponentStorage<RelationLinks<T>>)storage)[row].Targets!.ContainsKey(target);
    }

    /// <inheritdoc/>
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
        if (found && IsTracked(TypeIndex<RelationLinks<T>>.Value))
            typed.MarkDirty(row, _currentTick);
        return ref edgeValue;
    }

    /// <inheritdoc/>
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

        if (IsTracked(TypeIndex<RelationLinks<T>>.Value))
            typed.MarkDirty(row, _currentTick);
        return ref edgeValue;
    }

    private static class EmptyRelation<T>
    {
        internal static readonly IReadOnlyDictionary<Entity, T> Targets = new Dictionary<Entity, T>();
        internal static readonly IReadOnlyCollection<Entity> Entities = Array.Empty<Entity>();
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<Entity, T> Targets<T>(Entity source) where T : struct, IRelation
    {
        RequireAlive(source);
        var (archetype, row) = _entityTable[source.Id];
        return archetype.Storages.TryGetValue(TypeIndex<RelationLinks<T>>.Value, out var storage)
            ? ((ComponentStorage<RelationLinks<T>>)storage)[row].Values
            : EmptyRelation<T>.Targets;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<Entity> Sources<T>(Entity target) where T : struct, IRelation
    {
        RequireAlive(target);
        var (archetype, row) = _entityTable[target.Id];
        return archetype.Storages.TryGetValue(TypeIndex<RelationBacklinks<T>>.Value, out var storage)
            ? ((ComponentStorage<RelationBacklinks<T>>)storage)[row].Values
            : EmptyRelation<T>.Entities;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public void Query<TAccess0>(ChunkAction<TAccess0> action) where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
    {
        foreach (var chunk in Internal.ChunkQuery<TAccess0>.Value.Resolve(this))
            action(chunk.Access<TAccess0>());
    }

    /// <inheritdoc/>
    public void Query<TAccess0, TAccess1>(ChunkAction<TAccess0, TAccess1> action)
        where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
        where TAccess1 : struct, IComponentAccessor<TAccess1>, allows ref struct
    {
        foreach (var chunk in Internal.ChunkQuery<TAccess0, TAccess1>.Value.Resolve(this))
            action(chunk.Access<TAccess0>(), chunk.Access<TAccess1>());
    }

    // Query<T0..T{QueryArity.Max-1}>() implementations are generated — see
    // src/Wyrd.Ecs.Generators/WorldQueryMembersGenerator.cs.

    /// <inheritdoc/>
    public IDisposable TrackChanges<T>() where T : struct, IComponent
    {
        var typeIndex = TypeIndex<T>.Value;
        _tracking.Register(typeIndex);
        return new TrackingHandle(this, typeIndex);
    }

    /// <inheritdoc/>
    public ChangedComponents<T> ReadChanges<T>(int sinceTick) where T : struct, IComponent =>
        new(GetMatchingArchetypes(Internal.QuerySignature<Ref<T>>.Value), sinceTick);

    private void UntrackChanges(int typeIndex) => _tracking.Unregister(typeIndex);

    private sealed class TrackingHandle : IDisposable
    {
        private readonly World _world;
        private readonly int _typeIndex;
        private bool _disposed;

        internal TrackingHandle(World world, int typeIndex)
        {
            _world = world;
            _typeIndex = typeIndex;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _world.UntrackChanges(_typeIndex);
        }
    }

    /// <summary>True when change tracking is currently on for <paramref name="typeIndex"/>.</summary>
    internal bool IsTracked(int typeIndex) => _tracking.IsTracked(typeIndex);

    private void RequireAlive(Entity entity)
    {
        if (!IsAlive(entity))
            throw new InvalidOperationException($"Entity {entity} is not alive.");
    }

    /// <summary>
    /// Resolves <paramref name="entity"/>'s current location in one entity-table read, or
    /// <c>false</c> if it isn't alive. The single-lookup counterpart to calling
    /// <see cref="IsAlive"/> and then indexing <c>_entityTable</c> separately — used by
    /// <see cref="CommandBuffer"/>'s apply-time delegates, each of which used to do both
    /// independently (and often a third lookup after that) for one queued operation.
    /// </summary>
    internal bool TryResolve(Entity entity, out EntityLocation location)
    {
        if (!IsAlive(entity)) { location = default; return false; }
        location = _entityTable[entity.Id];
        return true;
    }

    /// <summary>
    /// Only copies a storage when <paramref name="signature"/> still contains its type —
    /// naturally excludes a just-removed component's storage without a caller needing to
    /// name it, since <paramref name="signature"/> already reflects the removal. Each
    /// clone is created sized to the new archetype's own entity capacity directly, or it
    /// could end up smaller than the archetype's <see cref="Archetype.Entities"/> array,
    /// breaking the invariant <see cref="Archetype.EnsureCapacity"/> relies on the
    /// moment more than a handful of entities land in this archetype.
    /// </summary>
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

    /// <summary>
    /// Registers a brand-new, storage-less archetype under <paramref name="signature"/>
    /// and invalidates every archetype-set cache. Callers populate the returned
    /// archetype's storages themselves, either by copying a template archetype's
    /// (<see cref="GetOrCreateArchetype"/>) or by creating them directly for a known
    /// set of component types (the generated <c>PlaceReservedEntity{T...}</c> overloads).
    /// </summary>
    private Archetype CreateArchetype(ArchetypeSignature signature)
    {
        var created = new Archetype(signature, _archetypeCapacity);
        _archetypes[signature] = created;
        _queryCache.Clear();
        _filteredQueryCache.Clear();
        return created;
    }

    /// <summary>
    /// The total number of live entities across every archetype — O(archetype count),
    /// not O(entity count), since it just sums each archetype's own cached row count.
    /// A cheap, deliberately coarse size proxy the static parallel scheduler's executor
    /// uses to decide whether a stage is worth dispatching to the thread pool at all.
    /// </summary>
    internal int TotalEntityCount => _archetypes.Values.Sum(a => a.Count);

    /// <summary>
    /// Every archetype whose signature contains all of <paramref name="required"/>'s bits,
    /// cached per required set and invalidated whenever a new archetype is created. A
    /// query only needs to walk this array, not every archetype in the world.
    /// </summary>
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

    /// <summary>
    /// Same as <see cref="GetMatchingArchetypes(ArchetypeSignature)"/>, plus
    /// <paramref name="filter"/>'s <c>Without</c>/<c>Any</c> checks — kept as a separate
    /// overload/cache rather than folding <paramref name="filter"/> into the existing
    /// one, so the chunk-callback queries and <see cref="ReadChanges{T}"/> (which never
    /// filter) don't pay for a cache key that's always empty for them.
    /// </summary>
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

    /// <summary>
    /// Moves an entity from <paramref name="source"/> to <paramref name="target"/>,
    /// copying every one of <paramref name="source"/>'s components that
    /// <paramref name="target"/> also has (a removed component has no storage to
    /// copy into).
    /// </summary>
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
