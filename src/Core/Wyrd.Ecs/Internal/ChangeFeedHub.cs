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
    private int _structuralSubscriberCount;
    private IDisposable? _structuralSubscription;

    /// <summary>Test-only instrumentation: how many times <see cref="ScanType{T}"/> has run, i.e. once per distinct watched type per tick — not once per subscriber.</summary>
    internal int ScanCount;

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
        if (structuralEvents) EnsureStructuralSubscribed();
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

    private void EnsureStructuralSubscribed()
    {
        if (_structuralSubscriberCount == 0)
            _structuralSubscription = _world.ObserveStructuralChanges(new HubObserver(this));
        _structuralSubscriberCount++;
    }

    internal void AppendStructural(ChangeEntry entry)
    {
        foreach (var subscriber in _subscribers.Values)
            if (subscriber.WantsStructuralEvents)
                lock (subscriber.Lock) subscriber.Front.Add(entry);
    }

    private void ScanType<T>(int typeIndex, int sinceTick) where T : struct, IComponent
    {
        ScanCount++;
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
        ResetWatermark(_world.CurrentTick);
        _world.OnTickAdvanced += OnTickAdvanced;
    }

    private void OnTickAdvanced(int tick)
    {
        foreach (var scan in _scanners.Values)
            scan(_sinceTick);
        ResetWatermark(tick);
    }

    private void ResetWatermark(int tick) => _sinceTick = tick - 1;

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

        if (!subscriber.WantsStructuralEvents) return;
        _structuralSubscriberCount--;
        if (_structuralSubscriberCount > 0) return;
        _structuralSubscription?.Dispose();
        _structuralSubscription = null;
    }

    private sealed class HubObserver(ChangeFeedHub hub) : IStructuralChangeObserver
    {
        public void OnEntityCreated(Entity entity) =>
            hub.AppendStructural(new ChangeEntry(entity, Entity.Null, 0, hub._world.CurrentTick, ChangeKind.EntityCreated));

        public void OnEntityDestroyed(Entity entity) =>
            hub.AppendStructural(new ChangeEntry(entity, Entity.Null, 0, hub._world.CurrentTick, ChangeKind.EntityDestroyed));

        public void OnComponentAdded(Entity entity, int typeIndex) =>
            hub.AppendStructural(new ChangeEntry(entity, Entity.Null, typeIndex, hub._world.CurrentTick, ChangeKind.ComponentAdded));

        public void OnComponentRemoved(Entity entity, int typeIndex) =>
            hub.AppendStructural(new ChangeEntry(entity, Entity.Null, typeIndex, hub._world.CurrentTick, ChangeKind.ComponentRemoved));

        public void OnTagAdded(Entity entity, int typeIndex) { }
        public void OnTagRemoved(Entity entity, int typeIndex) { }

        public void OnRelationLinked(Entity source, Entity target, int typeIndex) =>
            hub.AppendStructural(new ChangeEntry(source, target, typeIndex, hub._world.CurrentTick, ChangeKind.RelationLinked));

        public void OnRelationUnlinked(Entity source, Entity target, int typeIndex) =>
            hub.AppendStructural(new ChangeEntry(source, target, typeIndex, hub._world.CurrentTick, ChangeKind.RelationUnlinked));
    }
}
