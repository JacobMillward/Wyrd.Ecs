namespace Wyrd.Ecs.Persistence.Continuous.Internal;

/// <summary>
/// Drives continuous persistence's two background threads: a WAL-writer thread that
/// drains <see cref="ChangeCapture"/> into WAL segments and fsyncs on a cadence, and a
/// checkpoint-merge thread that periodically rotates the current segment, folds every
/// now-closed segment into a new checkpoint via <see cref="CheckpointBuilder"/>, and
/// retires them. Constructing this opens the first WAL segment immediately, keyed by
/// the world's current tick, so a segment always exists from construction onward
/// regardless of how soon the first real cycle runs.
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

    private volatile bool _rotationRequested;
    private readonly ManualResetEventSlim _rotationDone = new(false);

    private readonly CancellationTokenSource _cts = new();
    private Thread? _walWriterThread;
    private Thread? _checkpointMergeThread;

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
            var changes = _capture.SwapBuffers();
            _segmentWriter.WriteRecords(changes);
            _segmentWriter.Flush();

            if (_rotationRequested)
            {
                // Only actually rotate if the tick has moved on since the current
                // segment opened — segments are named by starting tick, so rotating to
                // the same tick would collide with the file OpenSegmentAppend just
                // closed (CreateNew throws if it already exists). If nothing has
                // changed tick-wise, there's nothing to separate yet; skip the rotate
                // and let the merge proceed against the one segment that already
                // exists, exactly as if it were freshly rotated.
                if (_world.CurrentTick > _segmentWriter.CurrentSegmentStartTick)
                    _segmentWriter.Rotate(_world.CurrentTick);
                _rotationRequested = false;
                _rotationDone.Set();
            }
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
        }
    }

    /// <summary>
    /// Requests the WAL-writer's next cycle drain-and-rotate, waits for that to
    /// happen, merges every now-closed segment into a new checkpoint via
    /// <see cref="CheckpointBuilder.Build"/>, and deletes them — commit before retire,
    /// never the reverse, so a crash between the two leaves the just-retired segments'
    /// content still safely present in the new checkpoint. An I/O failure, or the
    /// writer thread not servicing the rotation request in time, is reported via the
    /// error callback rather than propagating. Callable directly for synchronous
    /// testing (paired with manual <see cref="WalWriteCycle"/> calls to service the
    /// rotation, since nothing else does so without a real writer thread running); the
    /// checkpoint-merge background thread (added in a later task) loops on this.
    /// </summary>
    internal void CheckpointMergeCycle()
    {
        try
        {
            _rotationDone.Reset();
            _rotationRequested = true;

            var timeout = TimeSpan.FromTicks(Math.Max(_options.FsyncInterval.Ticks * 10, TimeSpan.FromSeconds(10).Ticks));
            if (!_rotationDone.Wait(timeout))
            {
                _onError?.Invoke(new TimeoutException("The WAL-writer thread did not service a rotation request in time; skipping this checkpoint cycle."));
                return;
            }

            var rotatedTick = _segmentWriter.CurrentSegmentStartTick;
            var toRetire = _walStore.ListSegmentStartTicks()
                .Where(startTick => startTick != rotatedTick)
                .ToList();

            CheckpointBuilder.Build(_checkpointStore, _walStore, rotatedTick);

            foreach (var startTick in toRetire)
                _walStore.DeleteSegment(startTick);
        }
        catch (Exception ex)
        {
            _onError?.Invoke(ex);
        }
    }

    /// <summary>Starts the WAL-writer and checkpoint-merge background threads.</summary>
    internal void Start()
    {
        _walWriterThread = new Thread(WalWriterLoop) { IsBackground = true, Name = "Wyrd.Ecs Continuous WAL Writer" };
        _checkpointMergeThread = new Thread(CheckpointMergeLoop) { IsBackground = true, Name = "Wyrd.Ecs Continuous Checkpoint Merge" };
        _walWriterThread.Start();
        _checkpointMergeThread.Start();
    }

    private void WalWriterLoop()
    {
        // Always runs one more cycle after cancellation is requested, so nothing
        // captured before shutdown is left undrained — unlike the checkpoint-merge
        // loop below, which deliberately does not force a final merge.
        while (true)
        {
            WalWriteCycle();
            if (_cts.IsCancellationRequested) return;
            _cts.Token.WaitHandle.WaitOne(_options.FsyncInterval);
        }
    }

    private void CheckpointMergeLoop()
    {
        // Waits a full CheckpointInterval before its first cycle too, unlike the
        // WAL-writer loop above — running a merge immediately on Start would defeat
        // "checkpoint every N", and (caught by a test during implementation) could
        // race ahead of a short-lived session, retiring a segment before anything else
        // had a chance to observe it. If cancellation is what wakes the wait, this
        // exits without ever merging — no forced final checkpoint on stop.
        while (true)
        {
            _cts.Token.WaitHandle.WaitOne(_options.CheckpointInterval);
            if (_cts.IsCancellationRequested) return;
            CheckpointMergeCycle();
        }
    }

    /// <summary>
    /// Signals both threads to stop, joins the WAL-writer first (letting it finish
    /// draining and fsync one last time), then the checkpoint-merge thread (letting any
    /// in-flight merge finish rather than aborting it — a merge is idempotent and safe
    /// to let complete). Safe to call whether or not <see cref="Start"/> was ever
    /// called.
    /// </summary>
    public void Dispose()
    {
        _cts.Cancel();
        _walWriterThread?.Join();
        _checkpointMergeThread?.Join();
        _rotationDone.Dispose();
        _cts.Dispose();
    }
}
