using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

public sealed partial class World
{
    /// <summary>
    /// Default floor for a new archetype's dense arrays when <see cref="WorldBuilder.WithArchetypeCapacity"/>
    /// wasn't used. Moderate and workload-agnostic: big enough to skip early doubling
    /// steps without assuming a large-few-archetypes shape.
    /// </summary>
    internal const int DefaultArchetypeCapacity = 64;

    private readonly Dictionary<TypeBitSet, Archetype> _archetypes = new();

    // Archetype-set query caches are read from parallel stages, so they live behind one
    // atomically-swapped immutable snapshot: published dictionary instances are never mutated,
    // readers stay lock-free at plain-dictionary lookup speed, and every mutation (miss fill or
    // invalidation) serializes on _archetypeSetsGate. Serializing both writers prevents a stale
    // fill from resurrecting entries invalidated by an archetype creation that interleaved with
    // it. The unfiltered and filtered key spaces stay separate dictionaries inside the snapshot
    // so callers that never filter never hash a filter.
    private readonly Lock _archetypeSetsGate = new();
    private ArchetypeSetCaches _archetypeSets = ArchetypeSetCaches.Empty;

    private readonly Archetype _emptyArchetype;
    private readonly int _archetypeCapacity;

    /// <summary>Hot-path query: invokes <paramref name="action"/> once per matching archetype chunk with a <typeparamref name="TAccess0"/> accessor.</summary>
    public void Query<TAccess0>(ChunkAction<TAccess0> action) where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
    {
        foreach (var chunk in Internal.ChunkQuery<TAccess0>.Value.Resolve(this))
            action(chunk.Access<TAccess0>());
    }

    /// <summary>Two-component overload, using <see cref="ChunkAction{TAccess0, TAccess1}"/>.</summary>
    public void Query<TAccess0, TAccess1>(ChunkAction<TAccess0, TAccess1> action)
        where TAccess0 : struct, IComponentAccessor<TAccess0>, allows ref struct
        where TAccess1 : struct, IComponentAccessor<TAccess1>, allows ref struct
    {
        foreach (var chunk in Internal.ChunkQuery<TAccess0, TAccess1>.Value.Resolve(this))
            action(chunk.Access<TAccess0>(), chunk.Access<TAccess1>());
    }

    // World.Query<TAccess0>/Query<TAccess0,TAccess1> above are deliberately hand-written and
    // capped at arity 2: the zero-codegen chunk-callback tier, usable with no generator setup,
    // unlike the fluent Query<TShape> chain (Query.cs). For 3+ components, use the fluent chain.

    /// <summary>Only copies a storage when <paramref name="signature"/> still contains its type, so a just-removed component's storage is naturally excluded. Each clone is sized to the new archetype's capacity directly, matching the invariant <see cref="Archetype.EnsureCapacity"/> relies on.</summary>
    private Archetype GetOrCreateArchetype(TypeBitSet signature, Archetype templateSource)
    {
        if (_archetypes.TryGetValue(signature, out var existing)) return existing;

        var created = CreateArchetype(signature);
        foreach (var (typeIndex, sourceStorage) in templateSource.Storages)
        {
            if (signature.Contains(typeIndex))
                created.Storages[typeIndex] = sourceStorage.CreateEmpty(created.Entities.Length);
        }

        return created;
    }

    /// <summary>Registers a brand-new, storage-less archetype and invalidates every archetype-set cache. Callers populate the returned archetype's storages themselves.</summary>
    private Archetype CreateArchetype(TypeBitSet signature)
    {
        var created = new Archetype(signature, _archetypeCapacity);
        _archetypes[signature] = created;
        lock (_archetypeSetsGate)
            Volatile.Write(ref _archetypeSets, ArchetypeSetCaches.Empty);
        return created;
    }

    /// <summary>Total live entities across every archetype: O(archetype count), not O(entity count). A cheap, deliberately coarse size proxy the scheduler uses to decide whether a stage is worth dispatching to the thread pool.</summary>
    internal int TotalEntityCount => _archetypes.Values.Sum(a => a.Count);

    /// <summary>
    /// Immutable snapshot of both archetype-set caches. Instances are never mutated after
    /// publication, so lock-free readers always observe a consistent pair, and invalidation is
    /// a single allocation-free swap to <see cref="Empty"/>. The two key spaces stay separate
    /// dictionaries so callers that never filter never hash an <see cref="ArchetypeFilter"/>.
    /// </summary>
    private sealed class ArchetypeSetCaches
    {
        public static readonly ArchetypeSetCaches Empty = new(
            new Dictionary<TypeBitSet, Archetype[]>(),
            new Dictionary<(TypeBitSet Required, ArchetypeFilter Filter), Archetype[]>());

        private ArchetypeSetCaches(
            Dictionary<TypeBitSet, Archetype[]> unfiltered,
            Dictionary<(TypeBitSet Required, ArchetypeFilter Filter), Archetype[]> filtered)
        {
            Unfiltered = unfiltered;
            Filtered = filtered;
        }

        public Dictionary<TypeBitSet, Archetype[]> Unfiltered { get; }
        public Dictionary<(TypeBitSet Required, ArchetypeFilter Filter), Archetype[]> Filtered { get; }

        /// <summary>A new snapshot with <paramref name="unfiltered"/> replacing this one's unfiltered cache; the filtered cache is shared unchanged.</summary>
        public ArchetypeSetCaches WithUnfiltered(Dictionary<TypeBitSet, Archetype[]> unfiltered) => new(unfiltered, Filtered);

        /// <summary>A new snapshot with <paramref name="filtered"/> replacing this one's filtered cache; the unfiltered cache is shared unchanged.</summary>
        public ArchetypeSetCaches WithFiltered(Dictionary<(TypeBitSet Required, ArchetypeFilter Filter), Archetype[]> filtered) => new(Unfiltered, filtered);
    }

    /// <summary>Every archetype whose signature contains all of <paramref name="required"/>'s bits, cached per required set and invalidated whenever a new archetype is created. Hot path: one volatile reference load plus a plain dictionary lookup; misses take a gate and publish a new snapshot.</summary>
    internal Archetype[] GetMatchingArchetypes(TypeBitSet required)
    {
        var snapshot = Volatile.Read(ref _archetypeSets);
        if (snapshot.Unfiltered.TryGetValue(required, out var cached)) return cached;

        lock (_archetypeSetsGate)
        {
            snapshot = Volatile.Read(ref _archetypeSets);
            if (snapshot.Unfiltered.TryGetValue(required, out cached)) return cached;

            var matches = new List<Archetype>();
            foreach (var archetype in _archetypes.Values)
            {
                if (required.IsSubsetOf(archetype.Signature))
                    matches.Add(archetype);
            }

            // Copy-on-write: publish a fresh snapshot; never mutate instances readers may hold.
            var result = matches.ToArray();
            var unfiltered = new Dictionary<TypeBitSet, Archetype[]>(snapshot.Unfiltered.Count + 1);
            foreach (var entry in snapshot.Unfiltered) unfiltered[entry.Key] = entry.Value;
            unfiltered[required] = result;
            Volatile.Write(ref _archetypeSets, snapshot.WithUnfiltered(unfiltered));
            return result;
        }
    }

    /// <summary>Same as <see cref="GetMatchingArchetypes(TypeBitSet)"/>, plus <paramref name="filter"/>'s Without/Any checks. A separate key space so callers that never filter (chunk queries, <see cref="ReadChanges{T}"/>) don't pay for hashing a filter.</summary>
    internal Archetype[] GetMatchingArchetypes(TypeBitSet required, ArchetypeFilter filter)
    {
        var key = (required, filter);
        var snapshot = Volatile.Read(ref _archetypeSets);
        if (snapshot.Filtered.TryGetValue(key, out var cached)) return cached;

        lock (_archetypeSetsGate)
        {
            snapshot = Volatile.Read(ref _archetypeSets);
            if (snapshot.Filtered.TryGetValue(key, out cached)) return cached;

            var matches = new List<Archetype>();
            foreach (var archetype in _archetypes.Values)
            {
                if (required.IsSubsetOf(archetype.Signature) && filter.Matches(archetype.Signature))
                    matches.Add(archetype);
            }

            // Copy-on-write: publish a fresh snapshot; never mutate instances readers may hold.
            var result = matches.ToArray();
            var filtered = new Dictionary<(TypeBitSet Required, ArchetypeFilter Filter), Archetype[]>(snapshot.Filtered.Count + 1);
            foreach (var entry in snapshot.Filtered) filtered[entry.Key] = entry.Value;
            filtered[key] = result;
            Volatile.Write(ref _archetypeSets, snapshot.WithFiltered(filtered));
            return result;
        }
    }
}
