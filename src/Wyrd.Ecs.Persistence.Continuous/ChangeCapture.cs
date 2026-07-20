namespace Wyrd.Ecs.Persistence.Continuous;

/// <summary>
/// Owns the tick-driven capture step: enables change tracking for every type in the
/// given <see cref="ComponentCodecRegistry"/>, observes structural changes via
/// <see cref="Internal.StructuralChangeCapture"/>, and appends every result into a
/// double-buffered pair of lists, swapped by <see cref="SwapBuffers"/> — the seam a
/// background WAL-writer thread (built in a later plan) drains from, so the thread
/// driving <see cref="World.OnTickAdvanced"/> never blocks on I/O. <see cref="Dispose"/>
/// stops capture entirely: change tracking is disabled for every type, the structural
/// observer is unregistered, and the tick subscription is removed.
/// </summary>
internal sealed class ChangeCapture : IDisposable
{
    private readonly World _world;
    private readonly ComponentCodecRegistry _registry;
    private readonly List<IDisposable> _trackingHandles = [];
    private readonly IDisposable _structuralSubscription;
    private readonly object _lock = new();
    private List<CapturedWalEntry> _front = [];
    private List<CapturedWalEntry> _back = [];
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
        // during the tick capture was enabled in. EncodeChanges/ReadChanges filter on
        // tick > sinceTick, so CurrentTick - 1 is the first value that still includes it.
        _sinceTick = world.CurrentTick - 1;

        foreach (var codec in registry.All)
            _trackingHandles.Add(codec.EnableChangeTracking(world));

        _structuralSubscription = world.ObserveStructuralChanges(new Internal.StructuralChangeCapture(world, registry, Append));
        world.OnTickAdvanced += OnTickAdvanced;
    }

    private void OnTickAdvanced(int tick)
    {
        foreach (var codec in _registry.All)
            foreach (var change in codec.EncodeChanges(_world, _sinceTick))
                Append(new CapturedWalEntry(WalRecordKind.ComponentChanged, change.Tick, _world.GetPermanentId(change.Entity), change.Discriminator, change.SchemaHash, change.Data));

        _sinceTick = tick;
    }

    private void Append(CapturedWalEntry entry)
    {
        lock (_lock) _front.Add(entry);
    }

    /// <summary>
    /// Swaps the front and back buffers under the lock and returns the buffer just
    /// swapped out (everything captured since the previous call), for the caller to
    /// drain at its own pace with no lock held. The returned list is only safe to read
    /// until the next call to <see cref="SwapBuffers"/>, at which point it may be
    /// cleared and reused as the new front buffer.
    /// </summary>
    internal List<CapturedWalEntry> SwapBuffers()
    {
        lock (_lock)
        {
            _back.Clear();
            (_front, _back) = (_back, _front);
            return _back;
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
