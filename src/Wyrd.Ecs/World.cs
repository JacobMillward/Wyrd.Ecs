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
    private TrackingState _tracking = new();
    private readonly Archetype _emptyArchetype;
    private readonly int _archetypeCapacity;
    private readonly CommandBuffer _commands;

    private EntityTable _entityTable = new();
    private int _currentTick = 1;

    /// <summary>Creates a new, empty world with <see cref="DefaultArchetypeCapacity"/>. Use <see cref="WorldBuilder"/> to configure it.</summary>
    public World() : this(DefaultArchetypeCapacity) { }

    internal World(int archetypeCapacity)
    {
        _archetypeCapacity = archetypeCapacity;
        _emptyArchetype = new Archetype(ArchetypeSignature.Empty, archetypeCapacity);
        _archetypes[ArchetypeSignature.Empty] = _emptyArchetype;
        _commands = new CommandBuffer(this);
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

    /// <summary>Destroys an entity and all of its components. Called only via <see cref="CommandBuffer"/>.</summary>
    internal void DestroyEntity(Entity entity)
    {
        RequireAlive(entity);
        _entityTable.Destroy(entity.Id);
        NotifyEntityDestroyed(entity);
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

    /// <inheritdoc/>
    public void AdvanceTick() => _currentTick++;

    /// <inheritdoc/>
    public void ApplyCommands() => ApplyCommands(_commands);

    /// <inheritdoc/>
    public void ApplyCommands(CommandBuffer commands)
    {
        if (commands.World != this)
            throw new InvalidOperationException("This CommandBuffer was created for a different World.");

        commands.Apply();
    }

    /// <summary>Reserves a fresh entity id without placing it — see <see cref="Internal.EntityTable.Reserve"/>. Used only by <see cref="CommandBuffer.CreateEntity"/>.</summary>
    internal Entity ReserveEntity() => _entityTable.Reserve();

    /// <summary>Places a previously-reserved entity into the empty archetype, making it alive, and notifies observers. Used only by <see cref="CommandBuffer.CreateEntity"/>'s queued placement.</summary>
    internal void PlaceReservedEntity(Entity entity)
    {
        _entityTable.Place(entity, _emptyArchetype);
        NotifyEntityCreated(entity);
    }

    /// <summary>
    /// Adds <typeparamref name="T"/> to <paramref name="entity"/> and returns a
    /// tracked mutable reference to it. Throws if the entity already has the
    /// component. Called only via <see cref="CommandBuffer"/>.
    /// </summary>
    internal ref T AddComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        var typeIndex = TypeIndex<T>.Value;
        var (source, sourceRow) = _entityTable[entity.Id];
        if (source.Signature.Contains(typeIndex))
            throw new InvalidOperationException($"Entity {entity} already has component {typeof(T)}.");

        var (target, targetRow) = MoveViaAddEdge(entity, source, sourceRow, typeIndex);

        var storage = target.GetOrCreateStorage<T>();
        if (IsTracked(typeIndex))
            storage.MarkDirty(targetRow, _currentTick);
        NotifyComponentAdded(entity, typeIndex);
        return ref storage[targetRow];
    }

    /// <inheritdoc/>
    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        var (archetype, row) = _entityTable[entity.Id];
        if (!archetype.Storages.TryGetValue(TypeIndex<T>.Value, out var storage))
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T)}.");

        var typed = (ComponentStorage<T>)storage;
        if (IsTracked(TypeIndex<T>.Value))
            typed.MarkDirty(row, _currentTick);
        return ref typed[row];
    }

    /// <inheritdoc/>
    public EntityView this[Entity entity] => new(this, entity);

    /// <inheritdoc/>
    public bool TryGetComponent<T>(Entity entity, out T value) where T : struct, IComponent
    {
        RequireAlive(entity);
        var (archetype, row) = _entityTable[entity.Id];
        if (archetype.Storages.TryGetValue(TypeIndex<T>.Value, out var storage))
        {
            value = ((ComponentStorage<T>)storage)[row];
            return true;
        }

        value = default;
        return false;
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
        var (source, sourceRow) = _entityTable[entity.Id];
        if (!source.Signature.Contains(typeIndex)) return;

        MoveViaRemoveEdge(entity, source, sourceRow, typeIndex);
        NotifyComponentRemoved(entity, typeIndex);
    }

    /// <summary>Adds the tag at <paramref name="typeIndex"/> to <paramref name="entity"/>. Called only via <see cref="CommandBuffer"/> — see <see cref="RemoveComponent(Entity, int)"/> for why this takes a type index, not a type parameter.</summary>
    internal void AddTag(Entity entity, int typeIndex)
    {
        RequireAlive(entity);
        var (source, sourceRow) = _entityTable[entity.Id];
        if (source.Signature.Contains(typeIndex)) return;

        MoveViaAddEdge(entity, source, sourceRow, typeIndex);
        NotifyTagAdded(entity, typeIndex);
    }

    /// <summary>Removes the tag at <paramref name="typeIndex"/> from <paramref name="entity"/>, if present. Called only via <see cref="CommandBuffer"/> — see <see cref="RemoveComponent(Entity, int)"/> for why this takes a type index, not a type parameter.</summary>
    internal void RemoveTag(Entity entity, int typeIndex)
    {
        RequireAlive(entity);
        var (source, sourceRow) = _entityTable[entity.Id];
        if (!source.Signature.Contains(typeIndex)) return;

        MoveViaRemoveEdge(entity, source, sourceRow, typeIndex);
        NotifyTagRemoved(entity, typeIndex);
    }

    /// <summary>
    /// Shared by every add path (<see cref="AddComponent{T}"/>, <see cref="AddTag(Entity, int)"/>):
    /// looks up (or creates and caches) the archetype-add edge for <paramref name="typeIndex"/>
    /// and moves the entity onto it.
    /// </summary>
    private (Archetype Target, int Row) MoveViaAddEdge(Entity entity, Archetype source, int sourceRow, int typeIndex)
    {
        if (!source.TryGetAddEdge(typeIndex, out var target))
        {
            target = GetOrCreateArchetype(source.Signature.With(typeIndex), source);
            source.SetAddEdge(typeIndex, target);
        }

        var targetRow = MoveEntity(entity, source, sourceRow, target);
        return (target, targetRow);
    }

    /// <summary>
    /// Shared by every remove path (<see cref="RemoveComponent(Entity, int)"/>, <see cref="RemoveTag(Entity, int)"/>):
    /// looks up (or creates and caches) the archetype-remove edge for <paramref name="typeIndex"/>
    /// and moves the entity onto it.
    /// </summary>
    private (Archetype Target, int Row) MoveViaRemoveEdge(Entity entity, Archetype source, int sourceRow, int typeIndex)
    {
        if (!source.TryGetRemoveEdge(typeIndex, out var target))
        {
            target = GetOrCreateArchetype(source.Signature.Without(typeIndex), source);
            source.SetRemoveEdge(typeIndex, target);
        }

        var targetRow = MoveEntity(entity, source, sourceRow, target);
        return (target, targetRow);
    }

    /// <inheritdoc/>
    public bool HasTag<T>(Entity entity) where T : struct, ITag
    {
        RequireAlive(entity);
        return _entityTable[entity.Id].Archetype.Signature.Contains(TypeIndex<T>.Value);
    }

    /// <inheritdoc/>
    public IEnumerable<SerializedComponent> EnumerateAll(SerializerRegistry registry)
    {
        foreach (var archetype in _archetypes.Values)
        {
            if (archetype.Count == 0) continue;

            foreach (var (typeIndex, storage) in archetype.Storages)
            {
                if (!registry.TryGetByTypeIndex(typeIndex, out var registered)) continue;

                for (var row = 0; row < archetype.Count; row++)
                    yield return new SerializedComponent(archetype.Entities[row], registered.Discriminator, registered.SerializeRow(storage.RawItems, row));
            }
        }
    }

    /// <inheritdoc/>
    public void Query<TAccess0>(ChunkAction<TAccess0> action) where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
    {
        var typeIndex = TAccess0.TypeIndex;
        var tracked = IsTracked(typeIndex);
        foreach (var archetype in GetMatchingArchetypes(Internal.QuerySignature<TAccess0>.Value))
        {
            if (archetype.Count == 0) continue;

            var storage = archetype.Storages[typeIndex];
            action(TAccess0.CreateChunk(storage.RawItems, storage.RawLastMarkedTick, _currentTick, 0, archetype.Count, tracked));
        }
    }

    /// <inheritdoc/>
    public void Query<TAccess0, TAccess1>(ChunkAction<TAccess0, TAccess1> action)
        where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
        where TAccess1 : struct, IComponentAccessor<TAccess1>, allows ref struct
    {
        var index0 = TAccess0.TypeIndex;
        var index1 = TAccess1.TypeIndex;
        var tracked0 = IsTracked(index0);
        var tracked1 = IsTracked(index1);
        foreach (var archetype in GetMatchingArchetypes(Internal.QuerySignature<TAccess0, TAccess1>.Value))
        {
            if (archetype.Count == 0) continue;

            var storage0 = archetype.Storages[index0];
            var storage1 = archetype.Storages[index1];
            action(
                TAccess0.CreateChunk(storage0.RawItems, storage0.RawLastMarkedTick, _currentTick, 0, archetype.Count, tracked0),
                TAccess1.CreateChunk(storage1.RawItems, storage1.RawLastMarkedTick, _currentTick, 0, archetype.Count, tracked1));
        }
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
        return created;
    }

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
            _entityTable[moved.Id] = (source, sourceRow);

        _entityTable[entity.Id] = (target, targetRow);
        return targetRow;
    }
}
