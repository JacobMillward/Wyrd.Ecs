using System.Threading;

namespace Wyrd.Ecs;

public sealed partial class World
{
    private readonly Lock _eventChannelsGate = new();
    private object?[] _eventChannels = [];
    private readonly List<Internal.IEventChannel> _activeEventChannels = [];

    /// <summary>
    /// Appends <paramref name="value"/> to <typeparamref name="T"/>'s event channel,
    /// creating it on first use. Immediate - no <see cref="CommandBuffer"/> involved, since
    /// nothing about appending to an independent buffer can corrupt an in-progress
    /// archetype/query walk. Safe to call concurrently from several threads at once,
    /// including several systems in the same parallel stage emitting at once.
    /// </summary>
    public void Emit<T>(T value) where T : struct, IEvent =>
        GetOrCreateEventChannel<T>().Write(value);

    /// <summary>
    /// Creates a new <see cref="EventReader{T}"/> over <typeparamref name="T"/>'s event
    /// channel, creating the channel on first use. The reader only sees events emitted from
    /// this point on.
    /// </summary>
    public EventReader<T> CreateEventReader<T>() where T : struct, IEvent =>
        new(GetOrCreateEventChannel<T>());

    /// <summary>
    /// Gets this <see cref="World"/>'s <see cref="Internal.EventChannel{T}"/> for
    /// <typeparamref name="T"/>, creating and registering it in
    /// <see cref="_activeEventChannels"/> on first use. Safe to call without the gate:
    /// channels are created once and never replaced or removed, so a stale reader either
    /// sees the channel or misses and takes the locked path. Writes and reads against an
    /// already-created channel go through that channel's own separate lock, so different
    /// event types never contend with each other. Internal, not private, so tests can
    /// exercise the get-or-create race directly.
    /// </summary>
    internal Internal.EventChannel<T> GetOrCreateEventChannel<T>() where T : struct, IEvent
    {
        var typeIndex = Internal.TypeIndex<T>.Value;

        var channels = Volatile.Read(ref _eventChannels);
        if ((uint)typeIndex < (uint)channels.Length && channels[typeIndex] is Internal.EventChannel<T> existing)
            return existing;

        lock (_eventChannelsGate)
        {
            if ((uint)typeIndex < (uint)_eventChannels.Length && _eventChannels[typeIndex] is Internal.EventChannel<T> ready)
                return ready;

            Internal.ArrayGrowth.EnsureCapacity(ref _eventChannels, typeIndex + 1);
            var created = new Internal.EventChannel<T>();
            _eventChannels[typeIndex] = created;
            _activeEventChannels.Add(created);
            return created;
        }
    }
}
