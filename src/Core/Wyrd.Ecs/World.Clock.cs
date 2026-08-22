namespace Wyrd.Ecs;

/// <summary>
/// Time: the tick counter, wall-clock vs virtual time, time scale and pausing, and the
/// fixed-step/variable-cadence update loop that drives the scheduler.
/// </summary>
public sealed partial class World
{
    private int _currentTick = 1;

    private readonly Lock _clockLock = new();
    private double _timeScale = 1.0;
    private bool _isPaused;

    private readonly TimeSpan _fixedStep;
    private readonly int _maxSubstepsPerUpdate;
    private TimeSpan _accumulator;
    private TimeSpan _virtualElapsed;

    /// <summary>The current tick, starting at 1. Every tracked write stamps the row it touches with this value.</summary>
    public int CurrentTick => _currentTick;

    /// <summary>Raised at the end of <see cref="AdvanceTick"/> with the new tick value, letting a per-tick background behavior (continuous persistence's capture step) hook in without the caller's tick loop needing to know about it.</summary>
    public event Action<int>? OnTickAdvanced;

    /// <summary>Advances to the next tick.</summary>
    public void AdvanceTick()
    {
        _currentTick++;
        // Under the channels gate so a concurrent first-time Emit of a new event type cannot
        // Add to _activeEventChannels mid-walk. Ordering is safe: Emit holds only this gate
        // (its channel writes happen after release), and Swap takes each channel's own gate
        // inside it - gate-then-channel-gate, never the reverse.
        lock (_eventChannelsGate)
        {
            foreach (var channel in _activeEventChannels) channel.Swap();
        }
        OnTickAdvanced?.Invoke(_currentTick);
    }

    /// <summary>
    /// Wall-clock time: advances by the raw <c>delta</c> passed to <see cref="Update"/> every
    /// call, never affected by <see cref="TimeScale"/> or <see cref="IsPaused"/>. The <see cref="Time"/>
    /// a system receives via <see cref="EcsSystem.Execute"/> is a different, virtual clock;
    /// see that method's own doc comment.
    /// </summary>
    public Time RealTime { get; private set; }

    /// <summary>
    /// Multiplies real delta into virtual delta for every system's <see cref="Time"/>.
    /// Default <c>1.0</c>. Throws <see cref="ArgumentOutOfRangeException"/> on a negative
    /// value: time-reversal isn't a supported concept here. Independent of <see cref="IsPaused"/>:
    /// pausing never reads or writes this value, so <see cref="Resume"/> always continues at
    /// whatever scale was last set. Guarded by an internal lock, not just for external callers:
    /// sibling systems in the same parallel stage are already permitted to call back into
    /// <see cref="World"/> concurrently (see <see cref="ISystemScheduler"/>'s documented
    /// contract), and this is an ordinary field with no other protection, unlike component
    /// storage.
    /// </summary>
    public double TimeScale
    {
        get { lock (_clockLock) return _timeScale; }
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "TimeScale cannot be negative.");
            lock (_clockLock) _timeScale = value;
        }
    }

    /// <summary>True between a <see cref="Pause"/> call and the matching <see cref="Resume"/>. While true, every Variable-cadence system's virtual <see cref="Time.Delta"/> is <see cref="TimeSpan.Zero"/> and the fixed-step accumulator does not advance.</summary>
    public bool IsPaused { get { lock (_clockLock) return _isPaused; } }

    /// <summary>Freezes virtual time: every system's <see cref="Time.Delta"/> becomes <see cref="TimeSpan.Zero"/> until <see cref="Resume"/>. Never touches <see cref="TimeScale"/>'s stored value.</summary>
    public void Pause() { lock (_clockLock) _isPaused = true; }

    /// <summary>Un-freezes virtual time, continuing at whatever <see cref="TimeScale"/> was last set to.</summary>
    public void Resume() { lock (_clockLock) _isPaused = false; }

    /// <summary>
    /// <c>accumulator / fixedStep</c>, updated at the end of every <see cref="Update"/> call.
    /// Always live, including before any <see cref="SystemCadence.Fixed"/> system is ever
    /// registered: zero registered Fixed systems just means the accumulator loop's stage
    /// list happens to be empty, not a distinct dormant state. For consumer-side render
    /// interpolation only: Wyrd does not store or blend component state itself, and this
    /// value should never feed back into an authoritative component.
    /// </summary>
    public double FixedStepAlpha { get; private set; }

    /// <summary>
    /// Runs one iteration of every registered system (see <c>WorldBuilder.AddSystemCore</c>/the
    /// generated <c>AddSystem&lt;T&gt;()</c>), staged by the static parallel schedule computed
    /// at <see cref="WorldBuilder.Build"/> time. Single-threaded, non-reentrant:
    /// <see cref="RealTime"/>, <see cref="FixedStepAlpha"/>, and the internal accumulator have
    /// no lock, unlike <see cref="TimeScale"/>/<see cref="IsPaused"/>. Separate from the
    /// documented guarantee that a system's own <see cref="EcsSystem.Execute"/> may call
    /// <see cref="TimeScale"/>/<see cref="Pause"/>/<see cref="Resume"/>/<c>AddSystem</c>/<c>RemoveSystem</c>
    /// concurrently with sibling systems in the same parallel stage.
    /// </summary>
    public void Update(TimeSpan delta)
    {
        AdvanceTick();
        RealTime = new Time(delta, RealTime.Elapsed + delta);

        double scale;
        bool paused;
        lock (_clockLock) { scale = _timeScale; paused = _isPaused; }
        var effectiveDelta = paused ? TimeSpan.Zero : delta * scale;

        _accumulator = Min(_accumulator + effectiveDelta, _fixedStep * _maxSubstepsPerUpdate);
        while (_accumulator >= _fixedStep)
        {
            _virtualElapsed += _fixedStep;
            _executor.RunStages(this, new Time(_fixedStep, _virtualElapsed), SystemCadence.Fixed);
            _accumulator -= _fixedStep;
        }
        FixedStepAlpha = _accumulator / _fixedStep;

        _totalElapsed += effectiveDelta;
        _executor.RunStages(this, new Time(effectiveDelta, _totalElapsed), SystemCadence.Variable);
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}
