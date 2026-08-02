namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// Owns the tick-driven capture step: subscribes to every type in the given
/// <see cref="ComponentCodecRegistry"/>, observes structural and relation changes via
/// <see cref="Internal.StructuralChangeCapture"/>, and appends every result into
/// double-buffered pairs of lists, swapped by <see cref="SwapBuffers"/> so a background
/// WAL-writer thread can drain without blocking <see cref="World.OnTickAdvanced"/>.
///
/// <para>
/// Value changes are resolved to a permanent id synchronously, per tick, not lazily at
/// drain time: <see cref="World.GetPermanentId"/> can only resolve a still-alive entity,
/// and deferring resolution would let a later-tick destroy make an earlier,
/// still-undrained value change unresolvable, silently dropping it.
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

        // Subscribed after every codec's own Subscribe call above: multicast delegate order
        // means the hub's scan populates this tick's changes before this handler drains them.
        world.OnTickAdvanced += OnTickAdvanced;
    }

    private void OnTickAdvanced(int tick)
    {
        var batch = new List<PendingValueChange>();
        foreach (var (codec, subscription) in _valueSubscriptions)
            foreach (var entry in subscription.Drain())
            {
                // Subscribe(codec) also reports ComponentAdded/ComponentRemoved (Value is null
                // for those); only ValueChanged belongs here. Add is redundant with the value
                // it carries (captured via this scan next tick); remove is captured separately
                // by the structural observer.
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
    /// Swaps the front and back buffers and returns the pair just swapped out (everything
    /// captured since the previous call), safe to read with no lock held until the next
    /// <see cref="SwapBuffers"/> call, after which they may be cleared and reused.
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
