namespace Wyrd.Ecs.Internal;

/// <summary>
/// Everything <see cref="World"/> needs per tracked component type: its live
/// consumers and, cached alongside them, the archetypes that contain it. Combining
/// both into one lookup means <see cref="World"/>'s per-tick retention pass does one
/// dictionary lookup per tracked type, not two.
/// </summary>
internal sealed class TrackedType
{
    internal readonly List<IChangeConsumerHandle> Consumers = new();
    internal Archetype[]? CachedArchetypes;

    /// <summary>
    /// The minimum consumer tick retention last trimmed down to. Consumer ticks only
    /// move forward (<see cref="ChangeConsumer{T}.Advance"/> rejects going backward,
    /// and a newly registered consumer starts at the world's current tick, which is
    /// always at or ahead of any prior minimum), so this is a safe watermark: retention
    /// can skip this type entirely on ticks where the minimum hasn't moved past it.
    /// </summary>
    internal int LastTrimmedTick;
}
