using System.Threading;
using Wyrd.Ecs.Internal;

namespace Wyrd.Ecs;

public sealed partial class World
{
    /// <summary>
    /// Not synchronized: <see cref="ObserveStructuralChanges"/>/dispose of the returned
    /// handle only race safely if the caller serializes them itself.
    /// <see cref="Internal.ChangeFeedHub"/> is safe since it only ever registers once,
    /// under its own lock. A second, independent caller of
    /// <see cref="ObserveStructuralChanges"/> directly, from a different thread than
    /// another such caller, needs its own external synchronization.
    /// </summary>
    private readonly List<IStructuralChangeObserver> _structuralObservers = new();

    /// <summary>
    /// Registers <paramref name="observer"/> for every structural change from this point
    /// on. Dispose the returned handle to unregister. Tier 0: synchronous, zero-buffer,
    /// fires inline at the exact moment of mutation. For a buffered, per-type-scoped
    /// alternative, see <see cref="Subscribe{T}"/>. Not synchronized against another
    /// independent caller from a different thread; see <see cref="_structuralObservers"/>'s doc.
    /// </summary>
    public IDisposable ObserveStructuralChanges(IStructuralChangeObserver observer)
    {
        _structuralObservers.Add(observer);
        return new StructuralObserverHandle(this, observer);
    }

    private void UnobserveStructuralChanges(IStructuralChangeObserver observer) => _structuralObservers.Remove(observer);

    private void NotifyEntityCreated(Entity entity)
    {
        foreach (var observer in _structuralObservers)
            observer.OnEntityCreated(entity);
    }

    private void NotifyEntityDestroyed(Entity entity)
    {
        foreach (var observer in _structuralObservers)
            observer.OnEntityDestroyed(entity);
    }

    private void NotifyComponentAdded(Entity entity, int typeIndex)
    {
        foreach (var observer in _structuralObservers)
            observer.OnComponentAdded(entity, typeIndex);
    }

    private void NotifyComponentRemoved(Entity entity, int typeIndex)
    {
        foreach (var observer in _structuralObservers)
            observer.OnComponentRemoved(entity, typeIndex);
    }

    private void NotifyTagAdded(Entity entity, int typeIndex)
    {
        foreach (var observer in _structuralObservers)
            observer.OnTagAdded(entity, typeIndex);
    }

    private void NotifyTagRemoved(Entity entity, int typeIndex)
    {
        foreach (var observer in _structuralObservers)
            observer.OnTagRemoved(entity, typeIndex);
    }

    internal void NotifyRelationLinked(Entity source, Entity target, int typeIndex)
    {
        foreach (var observer in _structuralObservers)
            observer.OnRelationLinked(source, target, typeIndex);
    }

    internal void NotifyRelationUnlinked(Entity source, Entity target, int typeIndex)
    {
        foreach (var observer in _structuralObservers)
            observer.OnRelationUnlinked(source, target, typeIndex);
    }

    private sealed class StructuralObserverHandle : IDisposable
    {
        private readonly World _world;
        private readonly IStructuralChangeObserver _observer;
        private bool _disposed;

        internal StructuralObserverHandle(World world, IStructuralChangeObserver observer)
        {
            _world = world;
            _observer = observer;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _world.UnobserveStructuralChanges(_observer);
        }
    }

    private TrackingState _tracking = new();

    /// <summary>Turns change tracking on for <typeparamref name="T"/>. Dispose the returned handle to turn it back off once nothing else needs it. The only way to make <see cref="ReadChanges{T}"/> observe anything.</summary>
    internal IDisposable TrackChanges<T>() where T : struct, IComponent
    {
        var typeIndex = TypeIndex<T>.Value;
        _tracking.Register(typeIndex);
        return new TrackingHandle(this, typeIndex);
    }

    /// <summary>Every row of <typeparamref name="T"/> touched since <paramref name="sinceTick"/>, across every archetype containing it. Only observes writes made while <see cref="TrackChanges{T}"/> was registered. Internal: the primitive-tier value-change scan feeding <see cref="Subscribe{T}"/>; a consumer wanting "process only what changed" should use tag-based filtering (One-Frame Components), not this.</summary>
    internal ChangedComponents<T> ReadChanges<T>(int sinceTick) where T : struct, IComponent =>
        new(GetMatchingArchetypes(Internal.QuerySignature<Ref<T>>.Value), sinceTick);

    private void UntrackChanges(int typeIndex) => _tracking.Unregister(typeIndex);

    /// <summary>
    /// Marks <paramref name="storage"/>'s row <paramref name="row"/> dirty if
    /// <typeparamref name="T"/> is currently tracked, no-op otherwise. The single
    /// implementation of the "check tracked, then mark" idiom every tracked-access path
    /// otherwise repeated by hand.
    /// </summary>
    internal void MarkDirtyIfTracked<T>(ComponentStorage<T> storage, int row) where T : struct, IComponent
    {
        if (IsTracked(TypeIndex<T>.Value))
            storage.MarkDirty(row, _currentTick);
    }

    /// <summary>Range counterpart to <see cref="MarkDirtyIfTracked{T}(ComponentStorage{T}, int)"/>, used by <see cref="EntityTemplate.MakeSetter{T}"/>'s batch instantiation path.</summary>
    internal void MarkDirtyRangeIfTracked<T>(ComponentStorage<T> storage, int startRow, int count) where T : struct, IComponent
    {
        if (IsTracked(TypeIndex<T>.Value))
            storage.MarkDirtyRange(startRow, count, _currentTick);
    }

    private Internal.ChangeFeedHub? _changeFeedHub;

    /// <summary>Test-only visibility into the lazily-created change-feed hub.</summary>
    internal Internal.ChangeFeedHub? DebugChangeFeedHub => _changeFeedHub;

    /// <summary>
    /// Subscribes to every <typeparamref name="T"/> value change plus
    /// <typeparamref name="T"/> being added to or removed from an existing entity,
    /// reported through a private <see cref="ChangeSubscription"/> only this caller
    /// drains. The scan for <typeparamref name="T"/> runs at most once per tick no
    /// matter how many subscribers are watching it. For structural events with no
    /// buffering delay, see <see cref="ObserveStructuralChanges"/> instead.
    /// </summary>
    public ChangeSubscription Subscribe<T>() where T : struct, IComponent =>
        GetOrCreateChangeFeedHub().Subscribe<T>();

    /// <summary>
    /// Same as <see cref="Subscribe{T}"/>, for a caller that doesn't know its component
    /// type at compile time: a registry-driven consumer working from
    /// <see cref="ComponentCodecRegistry.All"/>, for one. Shares the same
    /// scan-per-type-per-tick as any <see cref="Subscribe{T}"/> call already watching
    /// the same type.
    /// </summary>
    public ChangeSubscription Subscribe(IComponentCodec codec) =>
        GetOrCreateChangeFeedHub().Subscribe(codec);

    /// <summary>
    /// Subscribes to just <typeparamref name="T"/>'s own add/remove events. Unlike
    /// <see cref="Subscribe{T}"/>, a tag carries no value, so there's nothing to report
    /// but presence changing.
    /// </summary>
    public ChangeSubscription SubscribeTag<T>() where T : struct, ITag =>
        GetOrCreateChangeFeedHub().SubscribeTag<T>();

    /// <summary>
    /// Subscribes to just <typeparamref name="T"/>'s own link/unlink events. No other
    /// relation type, no component value tracking (a relation edge's mutation is never
    /// scanned; it's pushed synchronously the moment it happens).
    /// </summary>
    public ChangeSubscription SubscribeRelation<T>() where T : struct, IRelation =>
        GetOrCreateChangeFeedHub().SubscribeRelation<T>();

    /// <summary>
    /// Subscribes to entity creation/destruction, world-scoped rather than type-scoped:
    /// an entity being created or destroyed isn't associated with any one component/tag/
    /// relation type, unlike every other <c>Subscribe*</c> entry point.
    /// </summary>
    public ChangeSubscription SubscribeEntityLifecycle() =>
        GetOrCreateChangeFeedHub().SubscribeEntityLifecycle();

    /// <summary>
    /// Thread-safe lazy init: every <c>Subscribe*</c> entry point is callable from any
    /// thread, so a plain <c>_changeFeedHub ??= new(...)</c> isn't safe here, since two
    /// threads racing the first subscribe could each construct their own hub and
    /// register their own structural observer. <see cref="LazyInitializer"/>'s
    /// <c>EnsureInitialized</c> guarantees at most one instance is ever published.
    /// </summary>
    private Internal.ChangeFeedHub GetOrCreateChangeFeedHub() =>
        LazyInitializer.EnsureInitialized(ref _changeFeedHub, () => new Internal.ChangeFeedHub(this));

    private sealed class TrackingHandle : IDisposable
    {
        private readonly World _world;
        private readonly int _typeIndex;
        private bool _disposed;

        internal TrackingHandle(World world, int typeIndex)
        {
            _world = world;
            _typeIndex = typeIndex;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _world.UntrackChanges(_typeIndex);
        }
    }

    /// <summary>True when change tracking is currently on for <paramref name="typeIndex"/>.</summary>
    internal bool IsTracked(int typeIndex) => _tracking.IsTracked(typeIndex);
}
