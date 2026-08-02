namespace Wyrd.Ecs.Internal;

/// <summary>
/// Per-<see cref="World"/> hub behind <see cref="World.Subscribe{T}"/>: scans each
/// distinct type at most once per tick no matter how many subscribers are watching it,
/// fanning each result out to every interested subscriber's own private buffer. See
/// <see cref="ChangeSubscription"/>'s own doc for why there's no shared retained log.
///
/// <para>
/// <see cref="ChangeSubscription.Drain"/> is documented as callable from any thread —
/// the intended shape for a consumer like a background WAL-writer thread — so
/// <see cref="Subscribe{T}"/>/<see cref="Unsubscribe"/> and the tick-driven scan/fan-out
/// path (both of which mutate or enumerate <see cref="_subscribers"/> and its sibling
/// bookkeeping dictionaries) must be safe against running concurrently with a
/// <c>Subscribe</c>/<c>Dispose</c> call from a different thread than the one advancing
/// the tick. <see cref="_lock"/> guards all of that; each <see cref="Subscriber"/>'s own
/// <see cref="Subscriber.Lock"/> is a separate, narrower lock guarding only that
/// subscriber's own double-buffer, acquired only while <see cref="_lock"/> is already
/// held (or, in <see cref="Drain"/>, after releasing it) — never the other way around,
/// so the two never deadlock against each other.
/// </para>
/// </summary>
internal sealed class ChangeFeedHub
{
    private readonly World _world;
    private readonly object _lock = new();
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

    private sealed class Subscriber(bool wantsStructuralEvents, int? relationTypeIndexFilter = null)
    {
        internal readonly HashSet<int> TypeIndexes = [];
        internal readonly bool WantsStructuralEvents = wantsStructuralEvents;

        /// <summary>
        /// Set only by <see cref="SubscribeRelation{T}"/>: this subscriber wants
        /// <see cref="ChangeKind.RelationLinked"/>/<see cref="ChangeKind.RelationUnlinked"/>
        /// entries for exactly this relation type's <see cref="Internal.TypeIndex{T}"/>,
        /// and nothing else — narrower than <see cref="WantsStructuralEvents"/>, which
        /// delivers every structural event kind for every type.
        /// </summary>
        internal readonly int? RelationTypeIndexFilter = relationTypeIndexFilter;

        internal bool ConsumesStructuralSlot => WantsStructuralEvents || RelationTypeIndexFilter is not null;

        internal readonly object Lock = new();
        internal List<ChangeEntry> Front = [];
        internal List<ChangeEntry> Back = [];
    }

    internal ChangeSubscription Subscribe<T>(bool structuralEvents) where T : struct, IComponent
    {
        lock (_lock)
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
    }

    internal ChangeSubscription Subscribe(IComponentCodec codec, bool structuralEvents)
    {
        lock (_lock)
        {
            var id = _nextId++;
            var subscriber = new Subscriber(structuralEvents);
            var typeIndex = codec.TypeIndex;
            subscriber.TypeIndexes.Add(typeIndex);
            _subscribers[id] = subscriber;

            EnsureTypeTrackedErased(codec, typeIndex);
            if (structuralEvents) EnsureStructuralSubscribed();
            EnsureTickSubscribed();

            return new ChangeSubscription(this, id);
        }
    }

    /// <summary>
    /// Subscribes to just <typeparamref name="T"/>'s own relation link/unlink events —
    /// no value-change tracking (relation edges aren't scanned; they're already
    /// pushed synchronously via <see cref="AppendStructural"/>), and no other
    /// structural event kind or relation type. Cheaper and more targeted than
    /// <see cref="Subscribe{T}"/>'s <c>structuralEvents: true</c>, which delivers every
    /// structural event kind for every type and requires an unrelated tracked
    /// component type just to open the subscription.
    /// </summary>
    internal ChangeSubscription SubscribeRelation<T>() where T : struct, IRelation
    {
        lock (_lock)
        {
            var id = _nextId++;
            var subscriber = new Subscriber(wantsStructuralEvents: false, relationTypeIndexFilter: TypeIndex<T>.Value);
            _subscribers[id] = subscriber;

            EnsureStructuralSubscribed();

            return new ChangeSubscription(this, id);
        }
    }

    private void EnsureTypeTracked<T>(int typeIndex) where T : struct, IComponent
    {
        _typeInterestCount[typeIndex] = _typeInterestCount.GetValueOrDefault(typeIndex) + 1;
        if (_trackingHandles.ContainsKey(typeIndex)) return;

        _trackingHandles[typeIndex] = _world.TrackChanges<T>();
        _scanners[typeIndex] = sinceTick => ScanType<T>(typeIndex, sinceTick);
    }

    private void EnsureTypeTrackedErased(IComponentCodec codec, int typeIndex)
    {
        _typeInterestCount[typeIndex] = _typeInterestCount.GetValueOrDefault(typeIndex) + 1;
        if (_trackingHandles.ContainsKey(typeIndex)) return;

        _trackingHandles[typeIndex] = codec.EnableChangeTracking(_world);
        _scanners[typeIndex] = sinceTick => ScanTypeErased(codec, typeIndex, sinceTick);
    }

    private void EnsureStructuralSubscribed()
    {
        if (_structuralSubscriberCount == 0)
            _structuralSubscription = _world.ObserveStructuralChanges(new HubObserver(this));
        _structuralSubscriberCount++;
    }

    internal void AppendStructural(ChangeEntry entry)
    {
        lock (_lock)
        {
            foreach (var subscriber in _subscribers.Values)
            {
                var wants = subscriber.WantsStructuralEvents ||
                    (subscriber.RelationTypeIndexFilter == entry.TypeIndex &&
                     entry.Kind is ChangeKind.RelationLinked or ChangeKind.RelationUnlinked);
                if (wants)
                    lock (subscriber.Lock) subscriber.Front.Add(entry);
            }
        }
    }

    private void ScanType<T>(int typeIndex, int sinceTick) where T : struct, IComponent
    {
        ScanCount++;
        foreach (var change in _world.ReadChanges<T>(sinceTick))
            FanOutValueChange(typeIndex, change.Entity, change.Tick, change.Value);
    }

    private void ScanTypeErased(IComponentCodec codec, int typeIndex, int sinceTick)
    {
        ScanCount++;
        foreach (var change in codec.ReadRawChanges(_world, sinceTick))
            FanOutValueChange(typeIndex, change.Entity, change.Tick, change.Value);
    }

    private void FanOutValueChange(int typeIndex, Entity entity, int tick, object value)
    {
        var entry = new ChangeEntry(entity, Entity.Null, typeIndex, tick, ChangeKind.ValueChanged, value);
        lock (_lock)
        {
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
        lock (_lock)
        {
            foreach (var scan in _scanners.Values)
                scan(_sinceTick);
            ResetWatermark(tick);
        }
    }

    private void ResetWatermark(int tick) => _sinceTick = tick - 1;

    internal IReadOnlyList<ChangeEntry> Drain(int id)
    {
        Subscriber subscriber;
        lock (_lock)
            subscriber = _subscribers[id];

        lock (subscriber.Lock)
        {
            subscriber.Back.Clear();
            (subscriber.Front, subscriber.Back) = (subscriber.Back, subscriber.Front);
            return subscriber.Back;
        }
    }

    internal void Unsubscribe(int id)
    {
        lock (_lock)
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

            if (!subscriber.ConsumesStructuralSlot) return;
            _structuralSubscriberCount--;
            if (_structuralSubscriberCount > 0) return;
            _structuralSubscription?.Dispose();
            _structuralSubscription = null;
        }
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
