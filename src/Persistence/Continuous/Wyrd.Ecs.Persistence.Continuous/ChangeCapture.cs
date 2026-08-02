namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// Owns the tick-driven capture step: subscribes to every type in the given
/// <see cref="ComponentCodecRegistry"/> via <see cref="World.Subscribe(IComponentCodec)"/>
/// (sharing the underlying scan with any other subscriber already watching the same
/// type — see <c>Wyrd.Ecs.Internal.ChangeFeedHub</c>), observes structural and relation
/// changes via <see cref="Internal.StructuralChangeCapture"/>, and appends every result
/// into double-buffered pairs of lists, swapped by <see cref="SwapBuffers"/> — the seam
/// a background WAL-writer thread drains from, so the thread driving
/// <see cref="World.OnTickAdvanced"/> never blocks on I/O.
///
/// <para>
/// Each of this instance's own value-change subscriptions is drained every tick, on the
/// same thread that raised <see cref="World.OnTickAdvanced"/> — not lazily, on whatever
/// later tick <see cref="SwapBuffers"/> happens to be called. <see cref="World.GetPermanentId"/>
/// can only resolve a still-alive entity; deferring that resolution to
/// <see cref="SwapBuffers"/> (called from a background thread, on its own cadence, an
/// arbitrary number of ticks later) would mean an entity destroyed in a later tick makes
/// its own earlier, still-undrained value change unresolvable, silently dropping that
/// whole drain cycle. Resolving per tick, synchronously, is what keeps every pending
/// value already carrying a permanent id — the same guarantee structural events already
/// have via <see cref="Internal.StructuralChangeCapture"/>'s own synchronous callback.
/// </para>
/// </summary>
internal sealed class ChangeCapture : IDisposable
{
    private readonly World _world;
    private readonly List<(IComponentCodec Codec, ChangeSubscription Subscription)> _valueSubscriptions = [];
    private readonly IDisposable _structuralSubscription;
    private readonly object _lock = new();
    private List<CapturedWalEntry> _frontReady = [];
    private List<CapturedWalEntry> _backReady = [];
    private List<PendingValueChange> _frontPending = [];
    private List<PendingValueChange> _backPending = [];
    private bool _disposed;

    internal ChangeCapture(World world, ComponentCodecRegistry registry)
    {
        _world = world;

        foreach (var codec in registry.All)
            _valueSubscriptions.Add((codec, world.Subscribe(codec)));

        _structuralSubscription = world.ObserveStructuralChanges(new Internal.StructuralChangeCapture(world, registry, AppendReady));

        // Subscribed after every codec above, so each codec's own Subscribe call has
        // already registered the shared hub's tick handler first — multicast delegate
        // invocation runs in subscription order, so the hub's scan always populates
        // this tick's changes before this handler drains them.
        world.OnTickAdvanced += OnTickAdvanced;
    }

    private void OnTickAdvanced(int tick)
    {
        var batch = new List<PendingValueChange>();
        foreach (var (codec, subscription) in _valueSubscriptions)
            foreach (var entry in subscription.Drain())
            {
                // Subscribe(codec) also reports ComponentAdded/ComponentRemoved (Value is
                // null for those) alongside ValueChanged — only the latter belongs here.
                // A component add is already redundant with the value it carries, captured
                // via this same ValueChanged scan next tick (see
                // Internal.StructuralChangeCapture.OnComponentAdded's own doc); a component
                // remove is captured separately, as its own WAL record, by that same
                // structural observer.
                if (entry.Kind != ChangeKind.ValueChanged) continue;
                batch.Add(new PendingValueChange(codec, entry.Tick, _world.GetPermanentId(entry.Entity), entry.Value!));
            }

        if (batch.Count > 0)
            lock (_lock) _frontPending.AddRange(batch);
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
        foreach (var (_, subscription) in _valueSubscriptions) subscription.Dispose();
        _structuralSubscription.Dispose();
    }
}
