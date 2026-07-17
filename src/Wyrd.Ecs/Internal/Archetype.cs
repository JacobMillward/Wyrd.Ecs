namespace Wyrd.Ecs.Internal;

/// <summary>
/// One archetype: every entity sharing this exact component/tag <see cref="Signature"/>,
/// stored as parallel dense arrays — one <see cref="ComponentStorage{T}"/> per component
/// type (tags contribute only to <see cref="Signature"/>, never get a storage entry) plus
/// <see cref="Entities"/> mapping row → the entity occupying it. For Phase 4, "chunk" and
/// "archetype" are the same thing — see the archetype-storage plan's Global Constraints.
/// </summary>
internal sealed class Archetype
{
    private Entity[] _entities = new Entity[4];
    private Dictionary<int, Archetype>? _addEdges;
    private Dictionary<int, Archetype>? _removeEdges;

    internal ArchetypeSignature Signature { get; }
    internal ArchetypeStorages Storages { get; } = new();
    internal Entity[] Entities => _entities;
    internal int Count { get; private set; }

    internal Archetype(ArchetypeSignature signature) => Signature = signature;

    /// <summary>The archetype already known to result from adding <paramref name="typeIndex"/> to this one, if any component or tag has taken that transition before.</summary>
    internal bool TryGetAddEdge(int typeIndex, out Archetype target)
    {
        if (_addEdges is not null && _addEdges.TryGetValue(typeIndex, out var existing))
        {
            target = existing;
            return true;
        }

        target = null!;
        return false;
    }

    internal void SetAddEdge(int typeIndex, Archetype target) =>
        (_addEdges ??= new Dictionary<int, Archetype>())[typeIndex] = target;

    /// <summary>The archetype already known to result from removing <paramref name="typeIndex"/> from this one, if any component or tag has taken that transition before.</summary>
    internal bool TryGetRemoveEdge(int typeIndex, out Archetype target)
    {
        if (_removeEdges is not null && _removeEdges.TryGetValue(typeIndex, out var existing))
        {
            target = existing;
            return true;
        }

        target = null!;
        return false;
    }

    internal void SetRemoveEdge(int typeIndex, Archetype target) =>
        (_removeEdges ??= new Dictionary<int, Archetype>())[typeIndex] = target;

    internal int AddRow(Entity entity)
    {
        EnsureCapacity(Count + 1);
        var row = Count;
        Entities[row] = entity;
        Count++;
        return row;
    }

    internal Entity RemoveRow(int row)
    {
        var lastRow = Count - 1;
        var movedEntity = Entity.Null;
        if (row != lastRow)
        {
            movedEntity = Entities[lastRow];
            Entities[row] = movedEntity;
        }

        foreach (var storage in Storages.Values)
            storage.SwapRemove(row, lastRow);

        Entities[lastRow] = Entity.Null;
        Count--;
        return movedEntity;
    }

    internal ComponentStorage<T> GetOrCreateStorage<T>() where T : struct, IComponent
    {
        var typeIndex = TypeIndex<T>.Value;
        if (Storages.TryGetValue(typeIndex, out var existing))
            return (ComponentStorage<T>)existing;

        var created = new ComponentStorage<T>();
        created.EnsureCapacity(Entities.Length);
        Storages[typeIndex] = created;
        return created;
    }

    private void EnsureCapacity(int capacity)
    {
        if (_entities.Length < capacity)
        {
            var newLength = Math.Max(capacity, _entities.Length * 2);
            Array.Resize(ref _entities, newLength);
        }

        foreach (var storage in Storages.Values)
            storage.EnsureCapacity(capacity);
    }
}
