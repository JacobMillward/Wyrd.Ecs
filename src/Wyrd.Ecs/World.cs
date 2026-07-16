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

    public ref T AddComponent<T>(Entity entity) where T : struct, IComponent =>
        throw new NotImplementedException();

    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent =>
        throw new NotImplementedException();

    public bool TryGetComponent<T>(Entity entity, out T value) where T : struct, IComponent =>
        throw new NotImplementedException();

    public bool HasComponent<T>(Entity entity) where T : struct, IComponent =>
        throw new NotImplementedException();

    public void RemoveComponent<T>(Entity entity) where T : struct, IComponent =>
        throw new NotImplementedException();

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
}
