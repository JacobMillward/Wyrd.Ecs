namespace Wyrd.Ecs.Internal;

/// <summary>
/// Per-<see cref="World"/> hub behind <see cref="World.Subscribe{T}"/> and its siblings:
/// scans each distinct component type at most once per tick no matter how many
/// subscribers are watching it, fanning each result out to every subscriber's own private
/// buffer. See <see cref="ChangeSubscription"/>'s own doc for why there's no shared
/// retained log.
///
/// <para>
/// Each subscription is scoped to one type index and one fixed set of
/// <see cref="ChangeKind"/>s. The exception is <see cref="World.SubscribeEntityLifecycle"/>,
/// whose <c>TypeIndex</c> is <c>null</c>, since creation/destruction isn't tied to a type.
/// Every event is matched and fanned out through one path
/// (<see cref="Subscriber.Matches"/>/<see cref="Publish"/>).
/// </para>
///
/// <para>
/// <see cref="ChangeSubscription.Drain"/> is callable from any thread, so it must be safe
/// against running concurrently with <see cref="Subscribe{T}"/>/<see cref="Unsubscribe"/> and
/// the tick-driven scan. <see cref="_lock"/> guards the hub's own bookkeeping only;
/// publishing and scanning run against immutable snapshots of the subscriber/scanner sets,
/// so an event fan-out never touches <see cref="_lock"/> and a tick's scans never block a
/// concurrent drain. Each <see cref="Subscriber.Lock"/> guards only that subscriber's
/// double-buffer and is always acquired alone - never while any other lock is held - so no
/// ordering between them can deadlock.
/// </para>
/// </summary>
internal sealed class ChangeFeedHub
{
    private readonly World _world;
    private readonly object _lock = new();
    private readonly Dictionary<int, Subscriber> _subscribers = [];
    private readonly Dictionary<int, int> _typeInterestCount = [];
    private readonly Dictionary<int, IDisposable> _trackingHandles = [];
    private readonly Dictionary<int, TypeScanner> _scanners = [];

    // Immutable snapshots, swapped in under _lock whenever membership changes and read
    // lock-free by Publish/OnTickAdvanced. A subscriber removed after a snapshot was taken
    // may still receive publishes until the next rebuild; its buffers are simply never
    // drained again.
    private Subscriber[] _subscriberSnapshot = [];
    private TypeScanner[] _scannerSnapshot = [];

    private int _nextId;
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
    /// One watched type's scanner plus its own watermark: the tick to scan from, advanced
    /// by whoever runs the scan. Owning the watermark per record is what lets scans run
    /// outside the bookkeeping lock without losing a subscriber that joined mid-scan - a
    /// type absent from the tick's snapshot simply keeps its seed watermark and catches up
    /// on the next tick, rather than having a shared watermark advance past its data.
    /// </summary>
    private sealed class TypeScanner(Action<int> run, int sinceTick)
    {
        internal readonly Action<int> Run = run;
        internal int SinceTick = sinceTick;
    }

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
            RebuildSnapshots();

            EnsureTypeTracked<T>(typeIndex);
            EnsureStructuralSubscribed(subscriber);
            EnsureTickSubscribed();

            RebuildSnapshots();
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
            RebuildSnapshots();

            EnsureTypeTrackedErased(codec, codec.TypeIndex);
            EnsureStructuralSubscribed(subscriber);
            EnsureTickSubscribed();

            RebuildSnapshots();
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
            RebuildSnapshots();

            EnsureStructuralSubscribed(subscriber);

            RebuildSnapshots();
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
            RebuildSnapshots();

            EnsureStructuralSubscribed(subscriber);

            RebuildSnapshots();
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
            RebuildSnapshots();

            EnsureStructuralSubscribed(subscriber);

            RebuildSnapshots();
            return new ChangeSubscription(this, id);
        }
    }

    private void EnsureTypeTracked<T>(int typeIndex) where T : struct, IComponent
    {
        _typeInterestCount[typeIndex] = _typeInterestCount.GetValueOrDefault(typeIndex) + 1;
        if (_trackingHandles.ContainsKey(typeIndex)) return;

        _trackingHandles[typeIndex] = _world.TrackChanges<T>();
        _scanners[typeIndex] = new TypeScanner(sinceTick => ScanType<T>(typeIndex, sinceTick), _world.CurrentTick - 1);
    }

    private void EnsureTypeTrackedErased(IComponentCodec codec, int typeIndex)
    {
        _typeInterestCount[typeIndex] = _typeInterestCount.GetValueOrDefault(typeIndex) + 1;
        if (_trackingHandles.ContainsKey(typeIndex)) return;

        var source = (IComponentChangeSource)codec;
        _trackingHandles[typeIndex] = source.EnableChangeTracking(_world);
        _scanners[typeIndex] = new TypeScanner(sinceTick => ScanTypeErased(source, typeIndex, sinceTick), _world.CurrentTick - 1);
    }

    /// <summary>Registers the raw structural observer, if not already registered, the first time any subscriber wants a non-<see cref="ChangeKind.ValueChanged"/> kind.</summary>
    private void EnsureStructuralSubscribed(Subscriber subscriber)
    {
        if (!subscriber.WantsAnyStructuralKind) return;

        if (_structuralSubscriberCount == 0)
            _structuralSubscription = _world.ObserveStructuralChanges(new HubObserver(this));
        _structuralSubscriberCount++;
    }

    /// <summary>
    /// The single fan-out path every <see cref="ChangeEntry"/> goes through, structural or
    /// value alike. Lock-free against the hub: iterates the immutable subscriber snapshot and
    /// takes only the matching subscribers' own buffer locks. Publishers are single-threaded
    /// (structural observers fire inline from structural mutation, scans run at tick
    /// advance), so per-subscriber delivery order is preserved.
    /// </summary>
    internal void Publish(ChangeEntry entry)
    {
        var snapshot = Volatile.Read(ref _subscriberSnapshot);
        foreach (var subscriber in snapshot)
            if (subscriber.Matches(entry))
                lock (subscriber.Lock) subscriber.Front.Add(entry);
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
        _world.OnTickAdvanced += OnTickAdvanced;
    }

    private void OnTickAdvanced(int tick)
    {
        // Scans run against the snapshot outside the bookkeeping lock: a scan can walk every
        // archetype containing its type and must not stall a concurrent Drain for its
        // duration. Each scanner owns its watermark, so a subscriber that joined mid-scan
        // (absent from this snapshot) keeps its seed and catches up next tick instead of
        // having a shared watermark advance past unseen data. Single-flight like
        // World.Update itself: two concurrent tick advances would re-deliver a batch.
        var scanners = Volatile.Read(ref _scannerSnapshot);
        foreach (var scanner in scanners)
        {
            var sinceTick = scanner.SinceTick;
            scanner.Run(sinceTick);
            scanner.SinceTick = tick - 1;
        }
    }

    /// <summary>Rebuilds the lock-free snapshots after any membership change; callers hold <see cref="_lock"/>.</summary>
    private void RebuildSnapshots()
    {
        _subscriberSnapshot = [.. _subscribers.Values];
        _scannerSnapshot = [.. _scanners.Values];
    }

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

            // Unconditional, not just on last-structural-out: a survivor-heavy unsubscribe
            // must still retire the removed subscriber's buffer and any scanner whose type
            // interest just hit zero, or disposed subscriptions keep receiving events and
            // ghost scans keep walking untracked types.
            RebuildSnapshots();

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
