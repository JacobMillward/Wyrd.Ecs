namespace Wyrd.Ecs.Internal;

/// <summary>
/// Per-<see cref="World"/> hub behind <see cref="World.Subscribe{T}"/>: scans each
/// distinct type at most once per tick no matter how many subscribers are watching it,
/// fanning each result out to every interested subscriber's own private buffer. See
/// <see cref="ChangeSubscription"/>'s own doc for why there's no shared retained log.
/// </summary>
internal sealed class ChangeFeedHub
{
    private readonly World _world;
    private readonly Dictionary<int, Subscriber> _subscribers = [];
    private readonly Dictionary<int, int> _typeInterestCount = [];
    private readonly Dictionary<int, IDisposable> _trackingHandles = [];
    private readonly Dictionary<int, Action<int>> _scanners = [];
    private int _nextId;
    private int _sinceTick;
    private bool _tickSubscribed;

    internal ChangeFeedHub(World world) => _world = world;

    private sealed class Subscriber(bool wantsStructuralEvents)
    {
        internal readonly HashSet<int> TypeIndexes = [];
        internal readonly bool WantsStructuralEvents = wantsStructuralEvents;
        internal readonly object Lock = new();
        internal List<ChangeEntry> Front = [];
        internal List<ChangeEntry> Back = [];
    }

    internal ChangeSubscription Subscribe<T>(bool structuralEvents) where T : struct, IComponent
    {
        var id = _nextId++;
        var subscriber = new Subscriber(structuralEvents);
        var typeIndex = TypeIndex<T>.Value;
        subscriber.TypeIndexes.Add(typeIndex);
        _subscribers[id] = subscriber;

        EnsureTypeTracked<T>(typeIndex);
        EnsureTickSubscribed();

        return new ChangeSubscription(this, id);
    }

    private void EnsureTypeTracked<T>(int typeIndex) where T : struct, IComponent
    {
        _typeInterestCount[typeIndex] = _typeInterestCount.GetValueOrDefault(typeIndex) + 1;
        if (_trackingHandles.ContainsKey(typeIndex)) return;

        _trackingHandles[typeIndex] = _world.TrackChanges<T>();
        _scanners[typeIndex] = sinceTick => ScanType<T>(typeIndex, sinceTick);
    }

    private void ScanType<T>(int typeIndex, int sinceTick) where T : struct, IComponent
    {
        foreach (var change in _world.ReadChanges<T>(sinceTick))
        {
            var entry = new ChangeEntry(change.Entity, Entity.Null, typeIndex, change.Tick, ChangeKind.ValueChanged);
            foreach (var subscriber in _subscribers.Values)
                if (subscriber.TypeIndexes.Contains(typeIndex))
                    lock (subscriber.Lock) subscriber.Front.Add(entry);
        }
    }

    private void EnsureTickSubscribed()
    {
        if (_tickSubscribed) return;
        _tickSubscribed = true;
        // CurrentTick - 1, not CurrentTick: ticks are coarse-grained, so a mutation made
        // later in the same tick this runs in would be missed by the first scan otherwise
        // (see the matching test's own doc comment for the full reasoning).
        _sinceTick = _world.CurrentTick - 1;
        _world.OnTickAdvanced += OnTickAdvanced;
    }

    private void OnTickAdvanced(int tick)
    {
        foreach (var scan in _scanners.Values)
            scan(_sinceTick);
        _sinceTick = tick;
    }

    internal IReadOnlyList<ChangeEntry> Drain(int id)
    {
        var subscriber = _subscribers[id];
        lock (subscriber.Lock)
        {
            subscriber.Back.Clear();
            (subscriber.Front, subscriber.Back) = (subscriber.Back, subscriber.Front);
            return subscriber.Back;
        }
    }

    internal void Unsubscribe(int id)
    {
        if (!_subscribers.Remove(id, out var subscriber)) return;

        foreach (var typeIndex in subscriber.TypeIndexes)
        {
            var remaining = _typeInterestCount[typeIndex] - 1;
            if (remaining > 0)
            {
                _typeInterestCount[typeIndex] = remaining;
                continue;
            }

            _typeInterestCount.Remove(typeIndex);
            _trackingHandles[typeIndex].Dispose();
            _trackingHandles.Remove(typeIndex);
            _scanners.Remove(typeIndex);
        }
    }
}
