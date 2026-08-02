namespace Wyrd.Ecs.Internal;

/// <summary>
/// Per-<see cref="World"/> hub behind <see cref="World.Subscribe{T}"/> and its siblings
/// (<see cref="World.SubscribeTag{T}"/>, <see cref="World.SubscribeRelation{T}"/>,
/// <see cref="World.SubscribeEntityLifecycle"/>, <see cref="World.Subscribe(IComponentCodec)"/>):
/// scans each distinct component type at most once per tick no matter how many
/// subscribers are watching it, fanning each result out to every interested subscriber's
/// own private buffer. See <see cref="ChangeSubscription"/>'s own doc for why there's no
/// shared retained log.
///
/// <para>
/// Each subscription is scoped to exactly one type index and one fixed set of
/// <see cref="ChangeKind"/>s, set by whichever <c>Subscribe*</c> entry point created it.
/// The one exception is <see cref="World.SubscribeEntityLifecycle"/>, whose
/// <c>TypeIndex</c> is <c>null</c>, since entity creation/destruction isn't associated
/// with any one type. Every event, structural or value, is matched and fanned out
/// through one path (<see cref="Subscriber.Matches"/>/<see cref="Publish"/>).
/// </para>
///
/// <para>
/// <see cref="ChangeSubscription.Drain"/> is callable from any thread, so
/// <see cref="Subscribe{T}"/>/<see cref="Unsubscribe"/> and the tick-driven scan/fan-out
/// path must be safe against running concurrently with it. <see cref="_lock"/> guards
/// the hub's own bookkeeping; each <see cref="Subscriber"/>'s own <see cref="Subscriber.Lock"/>
/// is a narrower lock guarding only that subscriber's double-buffer, always acquired
/// while <see cref="_lock"/> is already held or (in <see cref="Drain"/>) after releasing
/// it, never the other way around, so the two never deadlock.
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

    /// <summary>Every structural <see cref="ChangeKind"/>: a subscriber whose <see cref="Subscriber.WantedKinds"/> intersects this needs the raw <see cref="IStructuralChangeObserver"/> registered.</summary>
    private const ChangeKind StructuralKinds =
        ChangeKind.EntityCreated | ChangeKind.EntityDestroyed | ChangeKind.ComponentAdded | ChangeKind.ComponentRemoved |
        ChangeKind.TagAdded | ChangeKind.TagRemoved | ChangeKind.RelationLinked | ChangeKind.RelationUnlinked;

    /// <summary>Test-only instrumentation: how many times <see cref="ScanType{T}"/> has run, i.e. once per distinct watched type per tick, not once per subscriber.</summary>
    internal int ScanCount;

    internal ChangeFeedHub(World world) => _world = world;

    /// <summary>
    /// One subscription: which type index it's scoped to (<c>null</c> only for
    /// <see cref="World.SubscribeEntityLifecycle"/>) and which <see cref="ChangeKind"/>s
    /// it wants, fixed at creation by whichever <c>Subscribe*</c> entry point built it.
    /// </summary>
    private sealed class Subscriber(int? typeIndex, ChangeKind wantedKinds)
    {
        internal readonly int? TypeIndex = typeIndex;
        internal readonly ChangeKind WantedKinds = wantedKinds;

        internal readonly object Lock = new();
        internal List<ChangeEntry> Front = [];
        internal List<ChangeEntry> Back = [];

        /// <summary>True if this subscriber wants any event that only the raw structural observer can produce (everything except <see cref="ChangeKind.ValueChanged"/>).</summary>
        internal bool WantsAnyStructuralKind => (WantedKinds & StructuralKinds) != 0;

        /// <summary>True if <paramref name="entry"/> is one this subscriber asked for: the single matching path every event kind goes through, structural or value alike.</summary>
        internal bool Matches(ChangeEntry entry) =>
            WantedKinds.HasFlag(entry.Kind) && (TypeIndex is null || TypeIndex == entry.TypeIndex);
    }

    internal ChangeSubscription Subscribe<T>() where T : struct, IComponent
    {
        var typeIndex = TypeIndex<T>.Value;
        lock (_lock)
        {
            var id = _nextId++;
            var subscriber = new Subscriber(typeIndex, ChangeKind.ValueChanged | ChangeKind.ComponentAdded | ChangeKind.ComponentRemoved);
            _subscribers[id] = subscriber;

            EnsureTypeTracked<T>(typeIndex);
            EnsureStructuralSubscribed(subscriber);
            EnsureTickSubscribed();

            return new ChangeSubscription(this, id);
        }
    }

    internal ChangeSubscription Subscribe(IComponentCodec codec)
    {
        lock (_lock)
        {
            var id = _nextId++;
            var subscriber = new Subscriber(codec.TypeIndex, ChangeKind.ValueChanged | ChangeKind.ComponentAdded | ChangeKind.ComponentRemoved);
            _subscribers[id] = subscriber;

            EnsureTypeTrackedErased(codec, codec.TypeIndex);
            EnsureStructuralSubscribed(subscriber);
            EnsureTickSubscribed();

            return new ChangeSubscription(this, id);
        }
    }

    internal ChangeSubscription SubscribeTag<T>() where T : struct, ITag
    {
        lock (_lock)
        {
            var id = _nextId++;
            var subscriber = new Subscriber(TypeIndex<T>.Value, ChangeKind.TagAdded | ChangeKind.TagRemoved);
            _subscribers[id] = subscriber;

            EnsureStructuralSubscribed(subscriber);

            return new ChangeSubscription(this, id);
        }
    }

    internal ChangeSubscription SubscribeRelation<T>() where T : struct, IRelation
    {
        lock (_lock)
        {
            var id = _nextId++;
            var subscriber = new Subscriber(TypeIndex<T>.Value, ChangeKind.RelationLinked | ChangeKind.RelationUnlinked);
            _subscribers[id] = subscriber;

            EnsureStructuralSubscribed(subscriber);

            return new ChangeSubscription(this, id);
        }
    }

    internal ChangeSubscription SubscribeEntityLifecycle()
    {
        lock (_lock)
        {
            var id = _nextId++;
            var subscriber = new Subscriber(typeIndex: null, ChangeKind.EntityCreated | ChangeKind.EntityDestroyed);
            _subscribers[id] = subscriber;

            EnsureStructuralSubscribed(subscriber);

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

        var source = (IComponentChangeSource)codec;
        _trackingHandles[typeIndex] = source.EnableChangeTracking(_world);
        _scanners[typeIndex] = sinceTick => ScanTypeErased(source, typeIndex, sinceTick);
    }

    /// <summary>Registers the raw structural observer, if not already registered, the first time any subscriber wants a non-<see cref="ChangeKind.ValueChanged"/> kind.</summary>
    private void EnsureStructuralSubscribed(Subscriber subscriber)
    {
        if (!subscriber.WantsAnyStructuralKind) return;

        if (_structuralSubscriberCount == 0)
            _structuralSubscription = _world.ObserveStructuralChanges(new HubObserver(this));
        _structuralSubscriberCount++;
    }

    /// <summary>The single fan-out path every <see cref="ChangeEntry"/> goes through, structural or value alike.</summary>
    internal void Publish(ChangeEntry entry)
    {
        lock (_lock)
        {
            foreach (var subscriber in _subscribers.Values)
                if (subscriber.Matches(entry))
                    lock (subscriber.Lock) subscriber.Front.Add(entry);
        }
    }

    private void ScanType<T>(int typeIndex, int sinceTick) where T : struct, IComponent
    {
        ScanCount++;
        foreach (var change in _world.ReadChanges<T>(sinceTick))
            Publish(new ChangeEntry(change.Entity, Entity.Null, typeIndex, change.Tick, ChangeKind.ValueChanged, change.Value));
    }

    private void ScanTypeErased(IComponentChangeSource source, int typeIndex, int sinceTick)
    {
        ScanCount++;
        foreach (var change in source.ReadRawChanges(_world, sinceTick))
            Publish(new ChangeEntry(change.Entity, Entity.Null, typeIndex, change.Tick, ChangeKind.ValueChanged, change.Value));
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

            if (subscriber.TypeIndex is { } typeIndex && (subscriber.WantedKinds & (ChangeKind.ValueChanged | ChangeKind.ComponentAdded | ChangeKind.ComponentRemoved)) != 0
                && _trackingHandles.ContainsKey(typeIndex))
            {
                var remaining = _typeInterestCount[typeIndex] - 1;
                if (remaining > 0)
                {
                    _typeInterestCount[typeIndex] = remaining;
                }
                else
                {
                    _typeInterestCount.Remove(typeIndex);
                    _trackingHandles[typeIndex].Dispose();
                    _trackingHandles.Remove(typeIndex);
                    _scanners.Remove(typeIndex);
                }
            }

            if (!subscriber.WantsAnyStructuralKind) return;
            _structuralSubscriberCount--;
            if (_structuralSubscriberCount > 0) return;
            _structuralSubscription?.Dispose();
            _structuralSubscription = null;
        }
    }

    private sealed class HubObserver(ChangeFeedHub hub) : IStructuralChangeObserver
    {
        public void OnEntityCreated(Entity entity) =>
            hub.Publish(new ChangeEntry(entity, Entity.Null, null, hub._world.CurrentTick, ChangeKind.EntityCreated));

        public void OnEntityDestroyed(Entity entity) =>
            hub.Publish(new ChangeEntry(entity, Entity.Null, null, hub._world.CurrentTick, ChangeKind.EntityDestroyed));

        public void OnComponentAdded(Entity entity, int typeIndex) =>
            hub.Publish(new ChangeEntry(entity, Entity.Null, typeIndex, hub._world.CurrentTick, ChangeKind.ComponentAdded));

        public void OnComponentRemoved(Entity entity, int typeIndex) =>
            hub.Publish(new ChangeEntry(entity, Entity.Null, typeIndex, hub._world.CurrentTick, ChangeKind.ComponentRemoved));

        public void OnTagAdded(Entity entity, int typeIndex) =>
            hub.Publish(new ChangeEntry(entity, Entity.Null, typeIndex, hub._world.CurrentTick, ChangeKind.TagAdded));

        public void OnTagRemoved(Entity entity, int typeIndex) =>
            hub.Publish(new ChangeEntry(entity, Entity.Null, typeIndex, hub._world.CurrentTick, ChangeKind.TagRemoved));

        public void OnRelationLinked(Entity source, Entity target, int typeIndex) =>
            hub.Publish(new ChangeEntry(source, target, typeIndex, hub._world.CurrentTick, ChangeKind.RelationLinked));

        public void OnRelationUnlinked(Entity source, Entity target, int typeIndex) =>
            hub.Publish(new ChangeEntry(source, target, typeIndex, hub._world.CurrentTick, ChangeKind.RelationUnlinked));
    }
}
