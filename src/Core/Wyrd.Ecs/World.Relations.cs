using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

/// <summary>
/// Relation edges: the <see cref="RelationLinks{T}"/> (source-side) and
/// <see cref="RelationBacklinks{T}"/> (target-side) components, their create/remove/replace
/// paths, and edge queries. Edge payloads are read through the same tracked-ref contract as
/// ordinary component access.
/// </summary>
public sealed partial class World
{
    /// <summary>
    /// Returns a mutable reference to <paramref name="source"/>'s <see cref="RelationLinks{T}"/>,
    /// creating it (and moving <paramref name="source"/> onto the archetype that includes it) on
    /// first use. Called only via <see cref="CommandBuffer"/>'s relation ops and this class's own
    /// destroy-cascade path.
    /// </summary>
    internal ref RelationLinks<T> GetOrCreateRelationLinks<T>(Entity source, EntityLocation location) where T : struct, IRelation
    {
        var typeIndex = TypeIndex<RelationLinks<T>>.Value;
        ref var links = ref location.Archetype.Signature.Contains(typeIndex)
            ? ref GetComponent<RelationLinks<T>>(source, location)
            : ref AddComponent<RelationLinks<T>>(source, location);
        if (links.Targets is null) links = new RelationLinks<T>(new Dictionary<Entity, T>());
        return ref links;
    }

    /// <summary>Same as <see cref="GetOrCreateRelationLinks{T}"/>, for the reverse (<see cref="RelationBacklinks{T}"/>) side.</summary>
    internal ref RelationBacklinks<T> GetOrCreateRelationBacklinks<T>(Entity target, EntityLocation location) where T : struct, IRelation
    {
        var typeIndex = TypeIndex<RelationBacklinks<T>>.Value;
        ref var backlinks = ref location.Archetype.Signature.Contains(typeIndex)
            ? ref GetComponent<RelationBacklinks<T>>(target, location)
            : ref AddComponent<RelationBacklinks<T>>(target, location);
        if (backlinks.Sources is null) backlinks = new RelationBacklinks<T>(new HashSet<Entity>());
        return ref backlinks;
    }

    /// <summary>Removes <paramref name="target"/> from <paramref name="source"/>'s <see cref="RelationLinks{T}"/>, if present, removing the component entirely if that was its last edge. Resolves <paramref name="source"/>'s location itself, so it's always safe to call with a stale location.</summary>
    internal void RemoveRelationLink<T>(Entity source, Entity target) where T : struct, IRelation
    {
        if (!TryResolve(source, out var location)) return;
        var typeIndex = TypeIndex<RelationLinks<T>>.Value;
        if (!location.Archetype.Signature.Contains(typeIndex)) return;

        var links = GetComponent<RelationLinks<T>>(source, location);
        if (!links.Targets!.Remove(target)) return;
        if (links.Targets.Count == 0) RemoveComponent(source, location, typeIndex);
    }

    /// <summary>
    /// Same as <see cref="RemoveRelationLink{T}"/>, for the reverse (<see cref="RelationBacklinks{T}"/>)
    /// side. This is the single point that notifies <see cref="IStructuralChangeObserver.OnRelationUnlinked"/>
    /// for an edge removal, not <see cref="RemoveRelationLink{T}"/>: every caller that removes an edge
    /// calls this method somewhere in the process except <see cref="RelationBacklinks{T}"/>'s own
    /// cascade-remove (which notifies explicitly instead, since it deliberately skips this method; see
    /// its own doc).
    /// </summary>
    internal void RemoveRelationBacklink<T>(Entity target, Entity source) where T : struct, IRelation
    {
        if (!TryResolve(target, out var location)) return;
        var typeIndex = TypeIndex<RelationBacklinks<T>>.Value;
        if (!location.Archetype.Signature.Contains(typeIndex)) return;

        var backlinks = GetComponent<RelationBacklinks<T>>(target, location);
        if (!backlinks.Sources!.Remove(source)) return;
        NotifyRelationUnlinked(source, target, TypeIndex<T>.Value);
        if (backlinks.Sources.Count == 0) RemoveComponent(target, location, typeIndex);
    }

    /// <summary>
    /// For an <see cref="IExclusiveRelation"/> type, removes every existing target other than
    /// <paramref name="target"/> before <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/>'s
    /// apply-time op adds the new edge. Mutates <see cref="RelationLinks{T}.Targets"/> directly
    /// rather than going through <see cref="RemoveRelationLink{T}"/>, to avoid an archetype move
    /// followed immediately by another one re-adding it.
    /// </summary>
    internal void ReplaceExclusiveRelationTarget<T>(Entity source, Entity target) where T : struct, IRelation
    {
        if (!TryResolve(source, out var location)) return;
        var typeIndex = TypeIndex<RelationLinks<T>>.Value;
        if (!location.Archetype.Signature.Contains(typeIndex)) return;

        var targets = GetComponent<RelationLinks<T>>(source, location).Targets!;
        if (targets.Count == 0) return;

        if (targets.Count == 1)
        {
            foreach (var existingTarget in targets.Keys)
            {
                if (existingTarget == target) return; // re-adding the same target: nothing to replace
                RemoveRelationBacklink<T>(existingTarget, source);
                targets.Remove(existingTarget);
                return;
            }
        }

        // More than one existing target shouldn't happen for a type that's always been exclusive,
        // but could if T was only just marked IExclusiveRelation after edges already existed.
        // ToArray snapshots before mutating the same dictionary.
        foreach (var existingTarget in targets.Keys.ToArray())
        {
            if (existingTarget == target) continue;
            RemoveRelationBacklink<T>(existingTarget, source);
            targets.Remove(existingTarget);
        }
    }

    /// <summary>True if <paramref name="source"/> has a <typeparamref name="T"/> edge to <paramref name="target"/>.</summary>
    public bool HasRelation<T>(Entity source, Entity target) where T : struct, IRelation
    {
        RequireAlive(source);
        var (archetype, row) = _entityTable[source.Id];
        if (!archetype.Storages.TryGetValue(TypeIndex<RelationLinks<T>>.Value, out var storage)) return false;
        return ((ComponentStorage<RelationLinks<T>>)storage)[row].Targets!.ContainsKey(target);
    }

    /// <summary>
    /// Tracked mutable reference to the payload of <paramref name="source"/>'s <typeparamref name="T"/>
    /// edge to <paramref name="target"/>, with <paramref name="found"/> reporting whether it exists.
    /// Same ref-lifetime caveat as <see cref="GetComponent{T}(Entity)"/>: a later <c>AddRelation</c>
    /// for a different target of the same source/relation can silently detach this reference.
    /// </summary>
    public ref T TryGetRelation<T>(Entity source, Entity target, out bool found) where T : struct, IRelation
    {
        RequireAlive(source);
        var (archetype, row) = _entityTable[source.Id];
        if (!archetype.Storages.TryGetValue(TypeIndex<RelationLinks<T>>.Value, out var storage))
        {
            found = false;
            return ref Unsafe.NullRef<T>();
        }

        var typed = (ComponentStorage<RelationLinks<T>>)storage;
        ref var edgeValue = ref CollectionsMarshal.GetValueRefOrNullRef(typed[row].Targets!, target);
        found = !Unsafe.IsNullRef(ref edgeValue);
        if (found) MarkDirtyIfTracked(typed, row);
        return ref edgeValue;
    }

    /// <summary>
    /// Tracked mutable reference to the payload of <paramref name="source"/>'s existing
    /// <typeparamref name="T"/> edge to <paramref name="target"/>. Throws if no such edge exists.
    /// Never creates or removes an edge. Use <see cref="CommandBuffer.AddRelation{T}(Entity, Entity, T)"/>/
    /// <see cref="CommandBuffer.RemoveRelation{T}(Entity, Entity)"/> for that. Same ref-lifetime
    /// caveat as <see cref="TryGetRelation{T}"/>.
    /// </summary>
    public ref T GetRelation<T>(Entity source, Entity target) where T : struct, IRelation
    {
        RequireAlive(source);
        var (archetype, row) = _entityTable[source.Id];
        if (!archetype.Storages.TryGetValue(TypeIndex<RelationLinks<T>>.Value, out var storage))
            throw new InvalidOperationException($"Entity {source} has no {typeof(T)} edges.");

        var typed = (ComponentStorage<RelationLinks<T>>)storage;
        ref var edgeValue = ref CollectionsMarshal.GetValueRefOrNullRef(typed[row].Targets!, target);
        if (Unsafe.IsNullRef(ref edgeValue))
            throw new InvalidOperationException($"Entity {source} has no {typeof(T)} edge to {target}.");

        MarkDirtyIfTracked(typed, row);
        return ref edgeValue;
    }

    private static class EmptyRelation<T>
    {
        internal static readonly IReadOnlyDictionary<Entity, T> Targets = new Dictionary<Entity, T>();
        internal static readonly IReadOnlyCollection<Entity> Entities = Array.Empty<Entity>();
    }

    /// <summary>Every target <paramref name="source"/> has a <typeparamref name="T"/> edge to, and each edge's payload. Empty, not throwing, if none.</summary>
    public IReadOnlyDictionary<Entity, T> Targets<T>(Entity source) where T : struct, IRelation
    {
        RequireAlive(source);
        var (archetype, row) = _entityTable[source.Id];
        return archetype.Storages.TryGetValue(TypeIndex<RelationLinks<T>>.Value, out var storage)
            ? ((ComponentStorage<RelationLinks<T>>)storage)[row].Values
            : EmptyRelation<T>.Targets;
    }

    /// <summary>Every source entity with a <typeparamref name="T"/> edge to <paramref name="target"/>. Empty, not throwing, if none.</summary>
    public IReadOnlyCollection<Entity> Sources<T>(Entity target) where T : struct, IRelation
    {
        RequireAlive(target);
        var (archetype, row) = _entityTable[target.Id];
        return archetype.Storages.TryGetValue(TypeIndex<RelationBacklinks<T>>.Value, out var storage)
            ? ((ComponentStorage<RelationBacklinks<T>>)storage)[row].Values
            : EmptyRelation<T>.Entities;
    }
}
