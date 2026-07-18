namespace Wyrd.Ecs.Internal;

/// <summary>
/// The world's entity identity table: generation-checked ids for liveness, permanent
/// opaque ids (see <see cref="EntityId"/>), and each live entity's current archetype
/// and row. Owns id allocation and recycling. A mutable struct, embedded directly in
/// <see cref="World"/> rather than a class, so hot-path location lookups don't pay for
/// an extra heap indirection to reach it — it's never referenced from anywhere but its
/// owning <see cref="World"/>, so it doesn't need reference semantics.
/// </summary>
internal struct EntityTable
{
    private EntityId[] _permanentIds = new EntityId[4];
    private int[] _generations = new int[4];
    private (Archetype Archetype, int Row)[] _locations = new (Archetype, int)[4];
    private readonly Stack<int> _freeIds = new();
    private int _nextId = 1; // Id 0 is reserved for Entity.Null.

    public EntityTable() { }

    /// <summary>The archetype+row currently backing entity id <paramref name="id"/>.</summary>
    internal ref (Archetype Archetype, int Row) this[int id] => ref _locations[id];

    internal EntityId PermanentId(int id) => _permanentIds[id];

    internal bool IsAlive(int id, int generation) =>
        id > 0 && id < _nextId && _generations[id] == generation;

    /// <summary>Allocates a fresh entity and places it in <paramref name="archetype"/>, returning its row there too.</summary>
    internal (Entity Entity, int Row) AllocateInto(Archetype archetype)
    {
        var entity = Allocate();
        var row = archetype.AddRow(entity);
        this[entity.Id] = (archetype, row);
        return (entity, row);
    }

    /// <summary>
    /// Removes entity <paramref name="id"/> from its current archetype, keeping the
    /// location table consistent for whichever entity backfilled its row, and retires
    /// the id for reuse.
    /// </summary>
    internal void Destroy(int id)
    {
        var (archetype, row) = this[id];
        var moved = archetype.RemoveRow(row);
        if (!moved.IsNull)
            this[moved.Id] = (archetype, row);

        Retire(id);
    }

    /// <summary>Allocates a fresh (recycled or new) id and permanent id, without placing it in any archetype.</summary>
    private Entity Allocate()
    {
        int id;
        if (_freeIds.Count > 0)
        {
            id = _freeIds.Pop();
        }
        else
        {
            id = _nextId++;
            EnsureCapacity(id);
        }

        _permanentIds[id] = EntityId.NewId();
        return new Entity(id, _generations[id]);
    }

    /// <summary>Retires <paramref name="id"/> for reuse, bumping its generation so stale handles report dead.</summary>
    private void Retire(int id)
    {
        _generations[id]++;
        _freeIds.Push(id);
    }

    private void EnsureCapacity(int id)
    {
        GrowableArray.EnsureCapacity(ref _generations, id + 1);
        GrowableArray.EnsureCapacity(ref _permanentIds, id + 1);
        GrowableArray.EnsureCapacity(ref _locations, id + 1);
    }
}
