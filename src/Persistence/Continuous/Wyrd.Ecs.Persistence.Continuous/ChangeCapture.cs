namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// Owns the tick-driven capture step: enables change tracking for every type in the
/// given <see cref="ComponentCodecRegistry"/>, observes structural changes via
/// <see cref="Internal.StructuralChangeCapture"/>, and appends every result into
/// double-buffered pairs of lists per kind, swapped by <see cref="SwapBuffers"/> — the
/// seam a background WAL-writer thread drains from, so the thread driving
/// <see cref="World.OnTickAdvanced"/> never blocks on I/O. Structural events are
/// captured as already-resolved <see cref="CapturedWalEntry"/> values (they have no
/// value to encode); component value changes are captured as unresolved
/// <see cref="PendingValueChange"/> values — deliberately not encoded here, since the
/// encode itself (whatever a registered <see cref="IComponentCodec"/> actually does,
/// which this package has no control over) is real work this method has no reason to
/// spend on the thread driving the simulation's tick. The scan that finds changed rows
/// still runs here, synchronously — it reads live component storage the very next tick
/// can overwrite, so deferring the scan itself (not just the encode) would risk reading
/// a later tick's value. <see cref="Dispose"/> stops capture entirely: change tracking
/// is disabled for every type, the structural observer is unregistered, and the tick
/// subscription is removed.
/// </summary>
internal sealed class ChangeCapture : IDisposable
{
    private readonly World _world;
    private readonly ComponentCodecRegistry _registry;
    private readonly List<IDisposable> _trackingHandles = [];
    private readonly IDisposable _structuralSubscription;
    private readonly object _lock = new();
    private List<CapturedWalEntry> _frontReady = [];
    private List<CapturedWalEntry> _backReady = [];
    private List<PendingValueChange> _frontPending = [];
    private List<PendingValueChange> _backPending = [];
    private int _sinceTick;
    private bool _disposed;

    internal ChangeCapture(World world, ComponentCodecRegistry registry)
    {
        _world = world;
        _registry = registry;
        // One less than the current tick, not the current tick itself: ticks are
        // coarse-grained (every mutation during tick N shares timestamp N, including
        // ones that happen after this constructor runs but before the first
        // AdvanceTick), so starting at CurrentTick would exclude anything dirty-marked
        // during the tick capture was enabled in. ReadRawChanges filters on
        // tick > sinceTick, so CurrentTick - 1 is the first value that still includes it.
        _sinceTick = world.CurrentTick - 1;

        foreach (var codec in registry.All)
            _trackingHandles.Add(codec.EnableChangeTracking(world));

        _structuralSubscription = world.ObserveStructuralChanges(new Internal.StructuralChangeCapture(world, registry, AppendReady));
        world.OnTickAdvanced += OnTickAdvanced;
    }

    private void OnTickAdvanced(int tick)
    {
        var batch = new List<PendingValueChange>();
        foreach (var codec in _registry.All)
            foreach (var change in codec.ReadRawChanges(_world, _sinceTick))
                batch.Add(new PendingValueChange(codec, change.Tick, _world.GetPermanentId(change.Entity), change.Value));

        if (batch.Count > 0)
            lock (_lock) _frontPending.AddRange(batch);

        _sinceTick = tick - 1;
    }

    private void AppendReady(CapturedWalEntry entry)
    {
        lock (_lock) _frontReady.Add(entry);
    }

    /// <summary>
    /// Swaps the front and back buffers under the lock and returns the pair just
    /// swapped out (everything captured since the previous call), for the caller to
    /// drain at its own pace with no lock held. The returned lists are only safe to
    /// read until the next call to <see cref="SwapBuffers"/>, at which point they may
    /// be cleared and reused as the new front buffers.
    /// </summary>
    internal DrainedChanges SwapBuffers()
    {
        lock (_lock)
        {
            _backReady.Clear();
            _backPending.Clear();
            (_frontReady, _backReady) = (_backReady, _frontReady);
            (_frontPending, _backPending) = (_backPending, _frontPending);
            return new DrainedChanges(_backReady, _backPending);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _world.OnTickAdvanced -= OnTickAdvanced;
        _structuralSubscription.Dispose();
        foreach (var handle in _trackingHandles) handle.Dispose();
    }
}
