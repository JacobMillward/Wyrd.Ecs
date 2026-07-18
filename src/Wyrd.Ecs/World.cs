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
    private TrackingState _tracking = new();
    private readonly Archetype _emptyArchetype;

    private EntityTable _entityTable = new();
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
        var (entity, _) = _entityTable.AllocateInto(_emptyArchetype);
        return entity;
    }

    /// <inheritdoc/>
    public void DestroyEntity(Entity entity)
    {
        RequireAlive(entity);
        _entityTable.Destroy(entity.Id);
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

    /// <summary>The live archetype collection, for <see cref="ChangeConsumer{T}"/> to build a <see cref="ChangeReadQuery{T}"/> from.</summary>
    internal Dictionary<ArchetypeSignature, Archetype>.ValueCollection Archetypes => _archetypes.Values;

    /// <inheritdoc/>
    public void AdvanceTick()
    {
        _currentTick++;
        _tracking.TrimRetiredEntries(_archetypes);
    }

    /// <inheritdoc/>
    public ref T AddComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        var typeIndex = TypeIndex<T>.Value;
        var (source, sourceRow) = _entityTable[entity.Id];
        if (source.Signature.Contains(typeIndex))
            throw new InvalidOperationException($"Entity {entity} already has component {typeof(T)}.");

        var (target, targetRow) = MoveViaAddEdge(entity, source, sourceRow, typeIndex);

        var storage = target.GetOrCreateStorage<T>();
        if (IsTracked(typeIndex))
            storage.MarkDirty(targetRow, entity, _currentTick);
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
            typed.MarkDirty(row, entity, _currentTick);
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

    /// <inheritdoc/>
    public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        var typeIndex = TypeIndex<T>.Value;
        var (source, sourceRow) = _entityTable[entity.Id];
        if (!source.Signature.Contains(typeIndex)) return;

        MoveViaRemoveEdge(entity, source, sourceRow, typeIndex);
    }

    /// <inheritdoc/>
    public void AddTag<T>(Entity entity) where T : struct, ITag
    {
        RequireAlive(entity);
        var typeIndex = TypeIndex<T>.Value;
        var (source, sourceRow) = _entityTable[entity.Id];
        if (source.Signature.Contains(typeIndex)) return;

        MoveViaAddEdge(entity, source, sourceRow, typeIndex);
    }

    /// <inheritdoc/>
    public void RemoveTag<T>(Entity entity) where T : struct, ITag
    {
        RequireAlive(entity);
        var typeIndex = TypeIndex<T>.Value;
        var (source, sourceRow) = _entityTable[entity.Id];
        if (!source.Signature.Contains(typeIndex)) return;

        MoveViaRemoveEdge(entity, source, sourceRow, typeIndex);
    }

    /// <summary>
    /// Shared by every add path (<see cref="AddComponent{T}"/>, <see cref="AddTag{T}"/>):
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
    /// Shared by every remove path (<see cref="RemoveComponent{T}"/>, <see cref="RemoveTag{T}"/>):
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
        var consumer = new ChangeConsumer<T>(this, typeIndex, _currentTick);
        _tracking.RegisterConsumer(typeIndex, consumer);
        return consumer;
    }

    /// <summary>Unregisters a <see cref="ChangeConsumer{T}"/>. Called only by <see cref="ChangeConsumer{T}.Dispose"/>.</summary>
    internal void UnregisterChangeConsumer<T>(int typeIndex, ChangeConsumer<T> consumer) where T : struct, IComponent =>
        _tracking.UnregisterConsumer(typeIndex, consumer);

    /// <summary>True when at least one consumer is currently registered for <paramref name="typeIndex"/>.</summary>
    internal bool IsTracked(int typeIndex) => _tracking.IsTracked(typeIndex);

    private void RequireAlive(Entity entity)
    {
        if (!IsAlive(entity))
            throw new InvalidOperationException($"Entity {entity} is not alive.");
    }

    /// <summary>
    /// Only copies a storage when <paramref name="signature"/> still contains its type —
    /// naturally excludes a just-removed component's storage without a caller needing to
    /// name it, since <paramref name="signature"/> already reflects the removal.
    /// </summary>
    private Archetype GetOrCreateArchetype(ArchetypeSignature signature, Archetype templateSource)
    {
        if (_archetypes.TryGetValue(signature, out var existing)) return existing;

        var created = CreateArchetype(signature);
        foreach (var (typeIndex, sourceStorage) in templateSource.Storages)
        {
            if (signature.Contains(typeIndex))
                created.Storages[typeIndex] = sourceStorage.CreateEmpty();
        }

        return created;
    }

    /// <summary>
    /// Registers a brand-new, storage-less archetype under <paramref name="signature"/>
    /// and invalidates every archetype-set cache. Callers populate the returned
    /// archetype's storages themselves, either by copying a template archetype's
    /// (<see cref="GetOrCreateArchetype"/>) or by creating them directly for a known
    /// set of component types (the generated <c>CreateEntity{T...}</c> overloads).
    /// </summary>
    private Archetype CreateArchetype(ArchetypeSignature signature)
    {
        var created = new Archetype(signature);
        _archetypes[signature] = created;
        _queryCache.Clear();
        _tracking.InvalidateCachedArchetypes();
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
                Array.Copy(sourceStorage.RawItems, sourceRow, targetStorage.RawItems, targetRow, 1);
        }

        var moved = source.RemoveRow(sourceRow);
        if (!moved.IsNull)
            _entityTable[moved.Id] = (source, sourceRow);

        _entityTable[entity.Id] = (target, targetRow);
        return targetRow;
    }
}
