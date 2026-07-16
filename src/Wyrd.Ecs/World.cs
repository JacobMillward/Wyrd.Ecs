using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// The concrete, real archetype-storage implementation of <see cref="IWorld"/>. See the
/// design's Core engine section — entities with identical component/tag sets share one
/// <see cref="Archetype"/>; adding/removing a component or tag moves the entity between
/// archetypes.
/// </summary>
public sealed class World : IWorld
{
    private readonly Dictionary<ArchetypeSignature, Archetype> _archetypes = new();
    private readonly Archetype _emptyArchetype;

    private EntityId[] _permanentIds = new EntityId[4];
    private int[] _generations = new int[4];
    private (Archetype Archetype, int Row)[] _locations = new (Archetype, int)[4];
    private readonly Stack<int> _freeIds = new();
    private int _nextId = 1; // Id 0 is reserved for Entity.Null.

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
    public ref T AddComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        var typeIndex = TypeIndex<T>.Value;
        var (source, sourceRow) = _locations[entity.Id];
        if (source.Signature.Contains(typeIndex))
            throw new InvalidOperationException($"Entity {entity} already has component {typeof(T)}.");

        var target = GetOrCreateArchetype(source.Signature.With(typeIndex), source);
        var targetRow = MoveEntity(entity, source, sourceRow, target);

        return ref target.GetOrCreateStorage<T>()[targetRow];
    }

    /// <inheritdoc/>
    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        var (archetype, row) = _locations[entity.Id];
        if (!archetype.Storages.TryGetValue(TypeIndex<T>.Value, out var storage))
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T)}.");

        return ref ((ComponentStorage<T>)storage)[row];
    }

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

        var target = GetOrCreateArchetype(source.Signature.Without(typeIndex), source, excludeTypeIndex: typeIndex);
        MoveEntity(entity, source, sourceRow, target);
    }

    public void AddTag<T>(Entity entity) where T : struct, ITag => throw new NotImplementedException();

    public void RemoveTag<T>(Entity entity) where T : struct, ITag => throw new NotImplementedException();

    public bool HasTag<T>(Entity entity) where T : struct, ITag => throw new NotImplementedException();

    public void Query<TAccess0>(ChunkAction<TAccess0> action) where TAccess0 : struct, IComponentAccessor =>
        throw new NotImplementedException();

    public void Query<TAccess0, TAccess1>(ChunkAction<TAccess0, TAccess1> action)
        where TAccess0 : struct, IComponentAccessor
        where TAccess1 : struct, IComponentAccessor =>
        throw new NotImplementedException();

    public EntityQuery<TAccess0> Query<TAccess0>() where TAccess0 : struct, IComponentAccessor =>
        throw new NotImplementedException();

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
        return created;
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
