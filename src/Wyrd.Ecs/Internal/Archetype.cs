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

    internal ArchetypeSignature Signature { get; }
    internal Dictionary<int, IComponentStorage> Storages { get; } = new();
    internal Entity[] Entities => _entities;
    internal int Count { get; private set; }

    internal Archetype(ArchetypeSignature signature) => Signature = signature;

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
