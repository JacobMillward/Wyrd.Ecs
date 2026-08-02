namespace Wyrd.Ecs;

public sealed partial class World
{
    private readonly Lock _eventChannelsGate = new();
    private object?[] _eventChannels = [];
    private readonly List<Internal.IEventChannel> _activeEventChannels = [];

    /// <summary>
    /// Appends <paramref name="value"/> to <typeparamref name="T"/>'s event channel,
    /// creating it on first use. Immediate — no <see cref="CommandBuffer"/> involved, since
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
    /// <see cref="_activeEventChannels"/> on first use. Locked for the whole check-or-create
    /// step: unlike <see cref="CommandBuffer.GetAddComponentBuffer{T}"/>, which relies on its
    /// caller already holding a lock, <see cref="Emit{T}"/> has no existing per-call lock to
    /// piggyback on. Every subsequent <c>Write</c>/<c>Read</c> call against an
    /// already-created channel goes through that channel's own separate lock instead, so two
    /// different event types never contend with each other here. Internal, not private, so
    /// tests can exercise the get-or-create race directly.
    /// </summary>
    internal Internal.EventChannel<T> GetOrCreateEventChannel<T>() where T : struct, IEvent
    {
        var typeIndex = Internal.TypeIndex<T>.Value;
        lock (_eventChannelsGate)
        {
            Internal.ArrayGrowth.EnsureCapacity(ref _eventChannels, typeIndex + 1);
            if (_eventChannels[typeIndex] is Internal.EventChannel<T> existing) return existing;

            var created = new Internal.EventChannel<T>();
            _eventChannels[typeIndex] = created;
            _activeEventChannels.Add(created);
            return created;
        }
    }
}
