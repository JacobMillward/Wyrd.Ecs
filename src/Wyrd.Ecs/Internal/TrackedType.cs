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
}
