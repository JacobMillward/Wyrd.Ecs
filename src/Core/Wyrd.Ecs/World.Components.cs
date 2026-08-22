using System.Runtime.CompilerServices;
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// Component and tag access and mutation on an existing entity: tracked-ref reads, add/remove
/// (via archetype edge moves), presence checks. Structural changes are called only via
/// <see cref="CommandBuffer"/>; the archetype-transition primitives they share live here too.
/// </summary>
public sealed partial class World
{
    /// <summary>Adds <typeparamref name="T"/> to <paramref name="entity"/> and returns a tracked mutable reference to it. Throws if the entity already has the component. Called only via <see cref="CommandBuffer"/>.</summary>
    internal ref T AddComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        return ref AddComponent<T>(entity, _entityTable[entity.Id]);
    }

    /// <summary>Same as <see cref="AddComponent{T}(Entity)"/>, for a caller that already resolved <paramref name="source"/> and shouldn't pay for a second entity-table read.</summary>
    internal ref T AddComponent<T>(Entity entity, EntityLocation source) where T : struct, IComponent
    {
        var typeIndex = TypeIndex<T>.Value;
        if (source.Archetype.Signature.Contains(typeIndex))
            throw new InvalidOperationException($"Entity {entity} already has component {typeof(T)}.");

        var target = MoveViaAddEdge(entity, source.Archetype, source.Row, typeIndex);

        var storage = target.Archetype.GetOrCreateStorage<T>();
        MarkDirtyIfTracked(storage, target.Row);
        NotifyComponentAdded(entity, typeIndex);
        return ref storage[target.Row];
    }

    /// <summary>
    /// Returns a tracked mutable reference to <paramref name="entity"/>'s <typeparamref name="T"/>.
    /// Throws if the entity doesn't have it. Do not hold the returned reference across a call to
    /// <see cref="ApplyCommands()"/>: a structural change applied afterward can silently
    /// invalidate it (a swap-remove can make it alias a different entity's data).
    /// </summary>
    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        return ref GetComponent<T>(entity, _entityTable[entity.Id]);
    }

    /// <summary>Same as <see cref="GetComponent{T}(Entity)"/>, for a caller that already resolved <paramref name="location"/> (avoids a second entity-table lookup).</summary>
    internal ref T GetComponent<T>(Entity entity, EntityLocation location) where T : struct, IComponent
    {
        if (!location.Archetype.Storages.TryGetValue(TypeIndex<T>.Value, out var storage))
            throw new InvalidOperationException($"Entity {entity} does not have component {typeof(T)}.");

        var typed = (ComponentStorage<T>)storage;
        MarkDirtyIfTracked(typed, location.Row);
        return ref typed[location.Row];
    }

    /// <summary>
    /// Same tracked-ref contract as <see cref="GetComponent{T}(Entity)"/>, with <paramref name="found"/>
    /// instead of a throw. When <paramref name="found"/> is false, the returned reference must not
    /// be dereferenced.
    /// </summary>
    public ref T TryGetComponent<T>(Entity entity, out bool found) where T : struct, IComponent
    {
        RequireAlive(entity);
        var (archetype, row) = _entityTable[entity.Id];
        if (!archetype.Storages.TryGetValue(TypeIndex<T>.Value, out var storage))
        {
            found = false;
            return ref Unsafe.NullRef<T>();
        }

        found = true;
        var typed = (ComponentStorage<T>)storage;
        MarkDirtyIfTracked(typed, row);
        return ref typed[row];
    }

    /// <summary>True if <paramref name="entity"/> has a <typeparamref name="T"/> component.</summary>
    public bool HasComponent<T>(Entity entity) where T : struct, IComponent
    {
        RequireAlive(entity);
        return _entityTable[entity.Id].Archetype.Signature.Contains(TypeIndex<T>.Value);
    }

    /// <summary>Removes the component at <paramref name="typeIndex"/> from <paramref name="entity"/>, if present. Called only via <see cref="CommandBuffer"/>, which already resolved the type index at its own call site: an archetype move only ever needs the index, not the type.</summary>
    internal void RemoveComponent(Entity entity, int typeIndex)
    {
        RequireAlive(entity);
        RemoveComponent(entity, _entityTable[entity.Id], typeIndex);
    }

    /// <summary>Same as <see cref="RemoveComponent(Entity, int)"/>, for a caller that already resolved <paramref name="source"/> (avoids a second entity-table lookup).</summary>
    internal void RemoveComponent(Entity entity, EntityLocation source, int typeIndex)
    {
        if (!source.Archetype.Signature.Contains(typeIndex)) return;

        MoveViaRemoveEdge(entity, source.Archetype, source.Row, typeIndex);
        NotifyComponentRemoved(entity, typeIndex);
    }

    /// <summary>Adds the tag at <paramref name="typeIndex"/> to <paramref name="entity"/>. Called only via <see cref="CommandBuffer"/>.</summary>
    internal void AddTag(Entity entity, int typeIndex)
    {
        RequireAlive(entity);
        AddTag(entity, _entityTable[entity.Id], typeIndex);
    }

    /// <summary>Same as <see cref="AddTag(Entity, int)"/>, for a caller that already resolved <paramref name="source"/> (avoids a second entity-table lookup).</summary>
    internal void AddTag(Entity entity, EntityLocation source, int typeIndex)
    {
        if (source.Archetype.Signature.Contains(typeIndex)) return;

        MoveViaAddEdge(entity, source.Archetype, source.Row, typeIndex);
        NotifyTagAdded(entity, typeIndex);
    }

    /// <summary>Removes the tag at <paramref name="typeIndex"/> from <paramref name="entity"/>, if present. Called only via <see cref="CommandBuffer"/>.</summary>
    internal void RemoveTag(Entity entity, int typeIndex)
    {
        RequireAlive(entity);
        RemoveTag(entity, _entityTable[entity.Id], typeIndex);
    }

    /// <summary>Same as <see cref="RemoveTag(Entity, int)"/>, for a caller that already resolved <paramref name="source"/> (avoids a second entity-table lookup).</summary>
    internal void RemoveTag(Entity entity, EntityLocation source, int typeIndex)
    {
        if (!source.Archetype.Signature.Contains(typeIndex)) return;

        MoveViaRemoveEdge(entity, source.Archetype, source.Row, typeIndex);
        NotifyTagRemoved(entity, typeIndex);
    }

    /// <summary>True if <paramref name="entity"/> has tag <typeparamref name="T"/>.</summary>
    public bool HasTag<T>(Entity entity) where T : struct, ITag
    {
        RequireAlive(entity);
        return _entityTable[entity.Id].Archetype.Signature.Contains(TypeIndex<T>.Value);
    }

    /// <summary>Shared by every add path: looks up (or creates and caches) the archetype-add edge for <paramref name="typeIndex"/> and moves the entity onto it.</summary>
    private EntityLocation MoveViaAddEdge(Entity entity, Archetype source, int sourceRow, int typeIndex)
    {
        if (!source.TryGetAddEdge(typeIndex, out var target))
        {
            target = GetOrCreateArchetype(source.Signature.With(typeIndex), source);
            source.SetAddEdge(typeIndex, target);
        }

        var targetRow = MoveEntity(entity, source, sourceRow, target);
        return new EntityLocation(target, targetRow);
    }

    /// <summary>Shared by every remove path: looks up (or creates and caches) the archetype-remove edge for <paramref name="typeIndex"/> and moves the entity onto it.</summary>
    private EntityLocation MoveViaRemoveEdge(Entity entity, Archetype source, int sourceRow, int typeIndex)
    {
        if (!source.TryGetRemoveEdge(typeIndex, out var target))
        {
            target = GetOrCreateArchetype(source.Signature.Without(typeIndex), source);
            source.SetRemoveEdge(typeIndex, target);
        }

        var targetRow = MoveEntity(entity, source, sourceRow, target);
        return new EntityLocation(target, targetRow);
    }

    /// <summary>Moves an entity from <paramref name="source"/> to <paramref name="target"/>, copying every component <paramref name="target"/> also has.</summary>
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
            _entityTable[moved.Id] = new EntityLocation(source, sourceRow);

        _entityTable[entity.Id] = new EntityLocation(target, targetRow);
        return targetRow;
    }
}
