namespace Wyrd.Ecs.Persistence.Continuous.Internal;

/// <summary>
/// Drives continuous persistence's two background threads: a WAL-writer thread that
/// drains <see cref="ChangeCapture"/> into WAL segments and fsyncs on a cadence, and a
/// checkpoint-merge thread that rotates the current segment, folds closed segments into
/// a new checkpoint via <see cref="CheckpointBuilder"/>, and retires them. Constructing
/// this opens the first WAL segment immediately, so one always exists from construction
/// onward.
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
    /// the error callback rather than propagating: a transient disk issue must not
    /// tear this thread down. Callable directly for synchronous testing; the WAL-writer
    /// background thread loops on this.
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
                // Skip if the tick hasn't moved since the segment opened: rotating to the
                // same tick would collide with the file just created (CreateNew throws if
                // it exists). The merge proceeds against the single existing segment
                // instead, as if freshly rotated.
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
    /// Requests the WAL-writer's next cycle drain-and-rotate, waits for it, merges every
    /// now-closed segment into a new checkpoint via <see cref="CheckpointBuilder.Build"/>,
    /// then deletes them. Commit before retire, never the reverse, so a crash between the
    /// two still leaves the retired segments' content in the checkpoint. Failures (I/O,
    /// or the writer not servicing the rotation in time) are reported via the error
    /// callback rather than propagating. Callable directly for synchronous testing,
    /// paired with manual <see cref="WalWriteCycle"/> calls to service the rotation; the
    /// checkpoint-merge background thread loops on this.
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
        // captured before shutdown is left undrained. Unlike the checkpoint-merge
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
        // Waits a full CheckpointInterval before its first cycle, unlike the WAL-writer
        // loop above: merging immediately on Start would defeat "checkpoint every N". No
        // forced final merge on cancellation either.
        while (true)
        {
            _cts.Token.WaitHandle.WaitOne(_options.CheckpointInterval);
            if (_cts.IsCancellationRequested) return;
            CheckpointMergeCycle();
        }
    }

    /// <summary>
    /// Signals both threads to stop, joins the WAL-writer first (letting it finish and
    /// fsync one last time), then the checkpoint-merge thread (letting any in-flight
    /// merge finish rather than aborting it). Safe to call whether or not
    /// <see cref="Start"/> was ever called.
    /// </summary>
    public void Dispose()
    {
        _cts.Cancel();
        _walWriterThread?.Join();
        _checkpointMergeThread?.Join();
        _rotationDone.Dispose();
        _cts.Dispose();
    }

    /// <summary>
    /// Closes the current WAL segment (no replacement is opened) and merges every
    /// segment into a new checkpoint stamped at the World's current tick, then retires
    /// them. Only safe to call after <see cref="Dispose"/>'s threads have stopped:
    /// closing the segment while the WAL-writer thread is still running would race it.
    /// </summary>
    internal void MergeFinalCheckpoint()
    {
        _segmentWriter.CloseCurrentSegment();
        var toRetire = _walStore.ListSegmentStartTicks().ToList();
        CheckpointBuilder.Build(_checkpointStore, _walStore, _world.CurrentTick);
        foreach (var startTick in toRetire)
            _walStore.DeleteSegment(startTick);
    }
}
