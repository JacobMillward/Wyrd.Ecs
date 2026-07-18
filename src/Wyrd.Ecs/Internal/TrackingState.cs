namespace Wyrd.Ecs.Internal;

/// <summary>
/// Change-tracking bookkeeping for every component type: which types currently have a
/// live <see cref="ChangeConsumer{T}"/>, and — for the ones that do — their consumer
/// list plus a cached matching-archetype list for retention's per-tick pass (see
/// <see cref="TrackedType"/>). <see cref="IsTracked"/> is a flat array read, not a
/// dictionary lookup, since it runs on every AddComponent/GetComponent/Query call —
/// the engine's hottest path. A mutable struct, embedded directly in <see cref="World"/>
/// rather than a class, so that hot-path check doesn't pay for an extra heap
/// indirection to reach it — it's never referenced from anywhere but its owning
/// <see cref="World"/>, so it doesn't need reference semantics.
/// </summary>
internal struct TrackingState
{
    private readonly Dictionary<int, TrackedType> _tracked = new();
    private int[] _consumerCounts = [];

    public TrackingState() { }

    internal bool IsTracked(int typeIndex) => typeIndex < _consumerCounts.Length && _consumerCounts[typeIndex] > 0;

    /// <summary>Registers <paramref name="consumer"/> as a reader of <paramref name="typeIndex"/>'s change log, turning tracking on for it.</summary>
    internal void RegisterConsumer(int typeIndex, IChangeConsumerHandle consumer)
    {
        GrowableArray.EnsureCapacity(ref _consumerCounts, typeIndex + 1);
        _consumerCounts[typeIndex]++;

        if (!_tracked.TryGetValue(typeIndex, out var state))
            _tracked[typeIndex] = state = new TrackedType();
        state.Consumers.Add(consumer);
    }

    /// <summary>Unregisters <paramref name="consumer"/>, turning tracking back off for <paramref name="typeIndex"/> if it was the last one.</summary>
    internal void UnregisterConsumer(int typeIndex, IChangeConsumerHandle consumer)
    {
        _consumerCounts[typeIndex]--;
        _tracked[typeIndex].Consumers.Remove(consumer);
    }

    /// <summary>Drops every tracked type's <see cref="TrackedType.CachedArchetypes"/>, e.g. when a new archetype is created.</summary>
    internal void InvalidateCachedArchetypes()
    {
        foreach (var state in _tracked.Values)
            state.CachedArchetypes = null;
    }

    /// <summary>
    /// Trims every tracked type's change log down to its slowest live consumer's
    /// position, once per tick. <paramref name="archetypes"/> is the world's live
    /// archetype registry, passed in rather than owned here since archetype creation
    /// stays <see cref="World"/>'s responsibility.
    /// </summary>
    internal void TrimRetiredEntries(Dictionary<ArchetypeSignature, Archetype> archetypes)
    {
        foreach (var (typeIndex, state) in _tracked)
        {
            if (state.Consumers.Count == 0) continue;

            var minTick = int.MaxValue;
            foreach (var consumer in state.Consumers)
                minTick = Math.Min(minTick, consumer.Tick);

            var matchingArchetypes = state.CachedArchetypes ??= ComputeArchetypesWithComponent(typeIndex, archetypes);
            foreach (var archetype in matchingArchetypes)
                archetype.Storages[typeIndex].TrimBefore(minTick);
        }
    }

    /// <summary>
    /// Every archetype whose signature contains <paramref name="typeIndex"/>. Every
    /// archetype returned here is guaranteed to have an <see cref="ArchetypeStorages"/>
    /// entry for <paramref name="typeIndex"/>, since only real component type indices
    /// (never tags) are ever tracked. The caller caches the result on the matching
    /// <see cref="TrackedType"/>.
    /// </summary>
    private static Archetype[] ComputeArchetypesWithComponent(int typeIndex, Dictionary<ArchetypeSignature, Archetype> archetypes)
    {
        var matches = new List<Archetype>();
        foreach (var archetype in archetypes.Values)
        {
            if (archetype.Signature.Contains(typeIndex))
                matches.Add(archetype);
        }

        return matches.ToArray();
    }
}
