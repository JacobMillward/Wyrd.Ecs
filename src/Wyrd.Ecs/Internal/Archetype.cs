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
    private Entity[] _entities;
    private Archetype?[] _addEdges = [];
    private Archetype?[] _removeEdges = [];

    internal ArchetypeSignature Signature { get; }
    internal ArchetypeStorages Storages { get; } = new();
    internal Entity[] Entities => _entities;
    internal int Count { get; private set; }

    internal Archetype(ArchetypeSignature signature, int initialCapacity)
    {
        Signature = signature;
        _entities = new Entity[initialCapacity];
    }

    /// <summary>
    /// The archetype already known to result from adding <paramref name="typeIndex"/> to
    /// this one, if any component or tag has taken that transition before. Indexed
    /// directly by <paramref name="typeIndex"/> (the same dense-small-int space
    /// <see cref="ArchetypeStorages"/> already indexes by) rather than hashed through a
    /// <c>Dictionary</c> — every lookup here is on the structural-change hot path.
    /// </summary>
    internal bool TryGetAddEdge(int typeIndex, out Archetype target)
    {
        if (typeIndex < _addEdges.Length && _addEdges[typeIndex] is { } existing)
        {
            target = existing;
            return true;
        }

        target = null!;
        return false;
    }

    internal void SetAddEdge(int typeIndex, Archetype target)
    {
        ArrayGrowth.EnsureCapacity(ref _addEdges, typeIndex + 1);
        _addEdges[typeIndex] = target;
    }

    /// <inheritdoc cref="TryGetAddEdge"/>
    internal bool TryGetRemoveEdge(int typeIndex, out Archetype target)
    {
        if (typeIndex < _removeEdges.Length && _removeEdges[typeIndex] is { } existing)
        {
            target = existing;
            return true;
        }

        target = null!;
        return false;
    }

    internal void SetRemoveEdge(int typeIndex, Archetype target)
    {
        ArrayGrowth.EnsureCapacity(ref _removeEdges, typeIndex + 1);
        _removeEdges[typeIndex] = target;
    }

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

    /// <summary>
    /// Grows <see cref="Entities"/> and every storage together, keeping storages sized
    /// to at least <see cref="Entities"/>'s length (the invariant <see cref="GetOrCreateStorage{T}"/>
    /// relies on). Skips the storages loop entirely when <see cref="Entities"/> is
    /// already large enough — otherwise every <see cref="AddRow"/> call would pay one
    /// virtual <see cref="IComponentStorage.EnsureCapacity"/> dispatch per component
    /// type even in the steady state where nothing actually grows.
    /// </summary>
    private void EnsureCapacity(int capacity)
    {
        if (_entities.Length >= capacity) return;

        ArrayGrowth.EnsureCapacity(ref _entities, capacity);

        foreach (var storage in Storages.Values)
            storage.EnsureCapacity(capacity);
    }
}
