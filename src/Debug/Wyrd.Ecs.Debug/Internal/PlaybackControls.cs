namespace Wyrd.Ecs.Debug.Internal;

/// <summary>
/// Thin wrapper over the already-shipped <see cref="World.Pause"/>/
/// <see cref="World.Resume"/>/<see cref="World.TimeScale"/>, raising
/// <see cref="Changed"/> so the playback POST endpoints (see <c>DebugServer</c>) and any
/// future push notification can react without polling.
/// </summary>
internal sealed class PlaybackControls(World world)
{
    public event Action? Changed;

    public bool IsPaused => world.IsPaused;

    public double TimeScale => world.TimeScale;

    public void Pause()
    {
        world.Pause();
        Changed?.Invoke();
    }

    public void Resume()
    {
        world.Resume();
        Changed?.Invoke();
    }

    public void SetTimeScale(double value)
    {
        world.TimeScale = value;
        Changed?.Invoke();
    }
}
