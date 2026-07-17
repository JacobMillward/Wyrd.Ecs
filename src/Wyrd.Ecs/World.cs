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
    private readonly Dictionary<ArchetypeSignature, Archetype> _archetypes = new();
    private readonly Dictionary<ArchetypeSignature, Archetype[]> _queryCache = new();
    private readonly Dictionary<int, Internal.TrackedType> _trackedTypes = new();
    private int[] _consumerCounts = [];
    private readonly Archetype _emptyArchetype;

    private EntityId[] _permanentIds = new EntityId[4];
    private int[] _generations = new int[4];
    private (Archetype Archetype, int Row)[] _locations = new (Archetype, int)[4];
    private readonly Stack<int> _freeIds = new();
    private int _nextId = 1; // Id 0 is reserved for Entity.Null.
    private int _currentTick = 1;

    /// <summary>Creates a new, empty world.</summary>
    public World()
    {
        _emptyArchetype = new Archetype(ArchetypeSignature.Empty);
        _archetypes[ArchetypeSignature.Empty] = _emptyArchetype;
    }

    /// <inheritdoc/>
    public Entity CreateEntity()
    {
        int id;
        if (_freeIds.Count > 0)
        {
            id = _freeIds.Pop();
        }
        else
        {
            id = _nextId++;
            EnsureIdCapacity(id);
        }

        _permanentIds[id] = EntityId.NewId();
        var entity = new Entity(id, _generations[id]);
        var row = _emptyArchetype.AddRow(entity);
        _locations[id] = (_emptyArchetype, row);

        return entity;
    }

    /// <inheritdoc/>
    public void DestroyEntity(Entity entity)
    {
        RequireAlive(entity);

        var (archetype, row) = _locations[entity.Id];
        var moved = archetype.RemoveRow(row);
        if (!moved.IsNull)
            _locations[moved.Id] = (archetype, row);

        _generations[entity.Id]++;
        _freeIds.Push(entity.Id);
    }

    /// <inheritdoc/>
    public bool IsAlive(Entity entity) =>
        entity.Id > 0 && entity.Id < _nextId && _generations[entity.Id] == entity.Generation;

    /// <inheritdoc/>
    public EntityId GetPermanentId(Entity entity)
    {
        RequireAlive(entity);
        return _permanentIds[entity.Id];
    }

    /// <inheritdoc/>
    public int CurrentTick => _currentTick;

    /// <summary>The live archetype collection, for <see cref="ChangeConsumer{T}"/> to build a <see cref="ChangeReadQuery{T}"/> from.</summary>
    internal Dictionary<ArchetypeSignature, Archetype>.ValueCollection Archetypes => _archetypes.Values;

    /// <inheritdoc/>
    public void AdvanceTick()
    {
        _currentTick++;
        TrimRetiredEntries();
    }

    private void TrimRetiredEntries()
    {
        foreach (var (typeIndex, state) in _trackedTypes)
        {
            if (state.Consumers.Count == 0) continue;

            var minTick = int.MaxValue;
            foreach (var consumer in state.Consumers)
                minTick = Math.Min(minTick, consumer.Tick);

            var archetypes = state.CachedArchetypes ??= ComputeArchetypesWithComponent(typeIndex);
            foreach (var archetype in archetypes)
                archetype.Storages[typeIndex].TrimBefore(minTick);
        }
    }

    /// <summary>
    /// Every archetype whose signature contains <paramref name="typeIndex"/>. Every
    /// archetype returned here is guaranteed to have a <see cref="Internal.ArchetypeStorages"/>
    /// entry for <paramref name="typeIndex"/>, since only real component type indices
    /// (never tags) are ever passed in here. The caller caches the result on the
    /// matching <see cref="Internal.TrackedType"/>.
    /// </summary>
    private Archetype[] ComputeArchetypesWithComponent(int typeIndex)
    {
        var matches = new List<Archetype>();
        foreach (var archetype in _archetypes.Values)
        {
            if (archetype.Signature.Contains(typeIndex))
                matches.Add(archetype);
        }

        return matches.ToArray();
    }

    /// <inheritdoc/>
    public ref T AddComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        var typeIndex = TypeIndex<T>.Value;
        var (source, sourceRow) = _locations[entity.Id];
        if (source.Signature.Contains(typeIndex))
            throw new InvalidOperationException($"Entity {entity} already has component {typeof(T)}.");

        if (!source.TryGetAddEdge(typeIndex, out var target))
        {
            target = GetOrCreateArchetype(source.Signature.With(typeIndex), source);
            source.SetAddEdge(typeIndex, target);
        }

        var targetRow = MoveEntity(entity, source, sourceRow, target);

        var storage = target.GetOrCreateStorage<T>();
        if (IsTracked(typeIndex))
            storage.MarkDirty(targetRow, entity, _currentTick);
        return ref storage[targetRow];
    }

    /// <inheritdoc/>
    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        var (archetype, row) = _locations[entity.Id];
        if (!archetype.Storages.TryGetValue(TypeIndex<T>.Value, out var storage))
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T)}.");

        var typed = (ComponentStorage<T>)storage;
        if (IsTracked(TypeIndex<T>.Value))
            typed.MarkDirty(row, entity, _currentTick);
        return ref typed[row];
    }

    /// <inheritdoc/>
    public EntityView this[Entity entity] => new(this, entity);

    /// <inheritdoc/>
    public bool TryGetComponent<T>(Entity entity, out T value) where T : struct, IComponent
    {
        RequireAlive(entity);
        var (archetype, row) = _locations[entity.Id];
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
        return _locations[entity.Id].Archetype.Signature.Contains(TypeIndex<T>.Value);
    }

    /// <inheritdoc/>
    public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        var typeIndex = TypeIndex<T>.Value;
        var (source, sourceRow) = _locations[entity.Id];
        if (!source.Signature.Contains(typeIndex)) return;

        if (!source.TryGetRemoveEdge(typeIndex, out var target))
        {
            target = GetOrCreateArchetype(source.Signature.Without(typeIndex), source, excludeTypeIndex: typeIndex);
            source.SetRemoveEdge(typeIndex, target);
        }

        MoveEntity(entity, source, sourceRow, target);
    }

    /// <inheritdoc/>
    public void AddTag<T>(Entity entity) where T : struct, ITag
    {
        RequireAlive(entity);
        var typeIndex = TypeIndex<T>.Value;
        var (source, sourceRow) = _locations[entity.Id];
        if (source.Signature.Contains(typeIndex)) return;

        if (!source.TryGetAddEdge(typeIndex, out var target))
        {
            target = GetOrCreateArchetype(source.Signature.With(typeIndex), source);
            source.SetAddEdge(typeIndex, target);
        }

        MoveEntity(entity, source, sourceRow, target);
    }

    /// <inheritdoc/>
    public void RemoveTag<T>(Entity entity) where T : struct, ITag
    {
        RequireAlive(entity);
        var typeIndex = TypeIndex<T>.Value;
        var (source, sourceRow) = _locations[entity.Id];
        if (!source.Signature.Contains(typeIndex)) return;

        if (!source.TryGetRemoveEdge(typeIndex, out var target))
        {
            target = GetOrCreateArchetype(source.Signature.Without(typeIndex), source);
            source.SetRemoveEdge(typeIndex, target);
        }

        MoveEntity(entity, source, sourceRow, target);
    }

    /// <inheritdoc/>
    public bool HasTag<T>(Entity entity) where T : struct, ITag
    {
        RequireAlive(entity);
        return _locations[entity.Id].Archetype.Signature.Contains(TypeIndex<T>.Value);
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
            var dirtyLog = tracked ? storage.GetDirtyLogForChunk(archetype.Entities, archetype.Count) : null!;
            action(TAccess0.CreateChunk(storage.RawItems, storage.RawLastMarkedTick, _currentTick, dirtyLog, 0, archetype.Count, tracked));
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
            var dirtyLog0 = tracked0 ? storage0.GetDirtyLogForChunk(archetype.Entities, archetype.Count) : null!;
            var dirtyLog1 = tracked1 ? storage1.GetDirtyLogForChunk(archetype.Entities, archetype.Count) : null!;
            action(
                TAccess0.CreateChunk(storage0.RawItems, storage0.RawLastMarkedTick, _currentTick, dirtyLog0, 0, archetype.Count, tracked0),
                TAccess1.CreateChunk(storage1.RawItems, storage1.RawLastMarkedTick, _currentTick, dirtyLog1, 0, archetype.Count, tracked1));
        }
    }

    // Query<T0..T{QueryArity.Max-1}>() implementations are generated — see
    // src/Wyrd.Ecs.Generators/WorldQueryMembersGenerator.cs.

    /// <inheritdoc/>
    public ChangeConsumer<T> RegisterChangeConsumer<T>() where T : struct, IComponent
    {
        var typeIndex = TypeIndex<T>.Value;
        EnsureConsumerCountCapacity(typeIndex + 1);
        _consumerCounts[typeIndex]++;

        var consumer = new ChangeConsumer<T>(this, typeIndex, _currentTick);
        if (!_trackedTypes.TryGetValue(typeIndex, out var state))
            _trackedTypes[typeIndex] = state = new Internal.TrackedType();
        state.Consumers.Add(consumer);

        return consumer;
    }

    /// <summary>Unregisters a <see cref="ChangeConsumer{T}"/>. Called only by <see cref="ChangeConsumer{T}.Dispose"/>.</summary>
    internal void UnregisterChangeConsumer<T>(int typeIndex, ChangeConsumer<T> consumer) where T : struct, IComponent
    {
        _consumerCounts[typeIndex]--;
        _trackedTypes[typeIndex].Consumers.Remove(consumer);
    }

    /// <summary>True when at least one consumer is currently registered for <paramref name="typeIndex"/>.</summary>
    internal bool IsTracked(int typeIndex) => typeIndex < _consumerCounts.Length && _consumerCounts[typeIndex] > 0;

    private void RequireAlive(Entity entity)
    {
        if (!IsAlive(entity))
            throw new InvalidOperationException($"Entity {entity} is not alive.");
    }

    private void EnsureIdCapacity(int id)
    {
        if (id < _generations.Length) return;
        var newLength = Math.Max(id + 1, _generations.Length * 2);
        Array.Resize(ref _generations, newLength);
        Array.Resize(ref _permanentIds, newLength);
        Array.Resize(ref _locations, newLength);
    }

    private void EnsureConsumerCountCapacity(int capacity)
    {
        if (_consumerCounts.Length >= capacity) return;
        Array.Resize(ref _consumerCounts, Math.Max(capacity, Math.Max(_consumerCounts.Length * 2, 4)));
    }

    private Archetype GetOrCreateArchetype(ArchetypeSignature signature, Archetype templateSource, int? excludeTypeIndex = null)
    {
        if (_archetypes.TryGetValue(signature, out var existing)) return existing;

        var created = new Archetype(signature);
        foreach (var (typeIndex, sourceStorage) in templateSource.Storages)
        {
            if (typeIndex == excludeTypeIndex) continue;
            created.Storages[typeIndex] = sourceStorage.CreateEmpty();
        }

        _archetypes[signature] = created;
        _queryCache.Clear();
        foreach (var state in _trackedTypes.Values)
            state.CachedArchetypes = null;
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
            _locations[moved.Id] = (source, sourceRow);

        _locations[entity.Id] = (target, targetRow);
        return targetRow;
    }
}
