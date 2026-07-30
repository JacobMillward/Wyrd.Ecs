namespace Wyrd.Ecs.Internal;

/// <summary>
/// One archetype: every entity sharing this exact component/tag <see cref="Signature"/>,
/// stored as parallel dense arrays — one <see cref="ComponentStorage{T}"/> per component
/// type (tags contribute only to <see cref="Signature"/>, never get a storage entry) plus
/// <see cref="Entities"/> mapping row → the entity occupying it. A query's "chunk" is one
/// archetype's full row range — there is no finer-grained chunking within an archetype.
/// </summary>
internal sealed class Archetype
{
    private Entity[] _entities;

    /// <summary>
    /// Cached archetype-transition targets, indexed directly by type index (the same
    /// dense-small-int space <see cref="ArchetypeStorages"/> already indexes by) rather
    /// than hashed through a <c>Dictionary</c> — every lookup here is on the
    /// structural-change hot path. One shared array for both add- and remove-edges: for
    /// a given type index, this archetype's <see cref="Signature"/> either contains it
    /// or doesn't, permanently, so <see cref="TryGetAddEdge"/> (only ever queried when
    /// it doesn't) and <see cref="TryGetRemoveEdge"/> (only ever queried when it does)
    /// can never both hold a value at the same slot.
    /// </summary>
    private Archetype?[] _edges = [];

    internal ArchetypeSignature Signature { get; }
    internal ArchetypeStorages Storages { get; } = new();
    internal Entity[] Entities => _entities;
    internal int Count { get; private set; }

    internal Archetype(ArchetypeSignature signature, int initialCapacity)
    {
        Signature = signature;
        _entities = new Entity[initialCapacity];
    }

    /// <summary>The archetype already known to result from adding <paramref name="typeIndex"/> to this one, if any component or tag has taken that transition before.</summary>
    internal bool TryGetAddEdge(int typeIndex, out Archetype target) => TryGetEdge(typeIndex, out target);

    internal void SetAddEdge(int typeIndex, Archetype target) => SetEdge(typeIndex, target);

    /// <summary>The archetype already known to result from removing <paramref name="typeIndex"/> from this one, if any component or tag has taken that transition before.</summary>
    internal bool TryGetRemoveEdge(int typeIndex, out Archetype target) => TryGetEdge(typeIndex, out target);

    internal void SetRemoveEdge(int typeIndex, Archetype target) => SetEdge(typeIndex, target);

    private bool TryGetEdge(int typeIndex, out Archetype target)
    {
        if (typeIndex < _edges.Length && _edges[typeIndex] is { } existing)
        {
            target = existing;
            return true;
        }

        target = null!;
        return false;
    }

    private void SetEdge(int typeIndex, Archetype target)
    {
        ArrayGrowth.EnsureCapacity(ref _edges, typeIndex + 1);
        _edges[typeIndex] = target;
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

    /// <summary>
    /// Bulk counterpart to <see cref="AddRow"/>: grows capacity once for the whole
    /// batch (instead of once per entity) and appends every entity in
    /// <paramref name="entities"/> starting at the current <see cref="Count"/>. Used by
    /// batch entity creation (<see cref="World.PlaceReservedEntities{T0}"/> and its
    /// higher-arity siblings) so a batch of N entities pays one capacity check and one
    /// contiguous copy, not N incremental ones.
    /// </summary>
    internal int AddRows(ReadOnlySpan<Entity> entities)
    {
        EnsureCapacity(Count + entities.Length);
        var startRow = Count;
        entities.CopyTo(Entities.AsSpan(startRow));
        Count += entities.Length;
        return startRow;
    }

    internal ComponentStorage<T> GetOrCreateStorage<T>() where T : struct, IComponent
    {
        var typeIndex = TypeIndex<T>.Value;
        if (Storages.TryGetValue(typeIndex, out var existing))
            return (ComponentStorage<T>)existing;

        var created = new ComponentStorage<T>(Entities.Length);
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
