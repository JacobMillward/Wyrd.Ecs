namespace Wyrd.Ecs.Persistence.Continuous.Internal;

/// <summary>
/// Drives continuous persistence's two background threads (the WAL-writer thread is
/// added to this class in this task; the checkpoint-merge thread, rotation
/// coordination, and <see cref="Start"/>/<see cref="Dispose"/> follow in later tasks).
/// Constructing this opens the first WAL segment immediately, keyed by
/// <paramref name="world"/>'s current tick, so a segment always exists from
/// construction onward regardless of how soon the first real cycle runs.
/// </summary>
internal sealed class ContinuousWalWorker : IDisposable
{
    private readonly World _world;
    private readonly ChangeCapture _capture;
    private readonly IPersistenceStore _checkpointStore;
    private readonly IWalStore _walStore;
    private readonly WalOptions _options;
    private readonly Action<Exception>? _onError;
    private readonly WalSegmentWriter _segmentWriter;

    internal ContinuousWalWorker(World world, ChangeCapture capture, IPersistenceStore checkpointStore, IWalStore walStore, WalOptions options, Action<Exception>? onError = null)
    {
        _world = world;
        _capture = capture;
        _checkpointStore = checkpointStore;
        _walStore = walStore;
        _options = options;
        _onError = onError;
        _segmentWriter = new WalSegmentWriter(walStore);
        _segmentWriter.EnsureSegmentOpen(world.CurrentTick);
    }

    /// <summary>
    /// Drains <see cref="ChangeCapture.SwapBuffers"/>, writes every entry to the
    /// currently open segment, and flushes it durably. An I/O failure is reported via
    /// the error callback rather than propagating — a transient disk issue must not
    /// tear this thread down. Callable directly for synchronous testing; the WAL-writer
    /// background thread (added in a later task) loops on this.
    /// </summary>
    internal void WalWriteCycle()
    {
        try
        {
            var entries = _capture.SwapBuffers();
            _segmentWriter.WriteRecords(entries);
            _segmentWriter.Flush();
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
        }
    }

    public void Dispose()
    {
    }
}
