namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Delays a resource's release until <see cref="FrameInFlightTracker.FramesInFlight"/> frames
/// have been submitted after it was retired, since an SDL_GPU resource can't be safely
/// destroyed while a still-in-flight command buffer might reference it. Holds the release
/// callback itself, not any resource identity, so callers own exactly what "releasing" means
/// (e.g. <c>SDL_ReleaseGPUTexture</c> vs <c>SDL_ReleaseGPUBuffer</c>); this class only owns
/// timing.
/// </summary>
public sealed class DeferredDestroyQueue
{
    private readonly Queue<(long FrameTag, Action Release)> _pending = new();

    /// <summary>Schedules <paramref name="release"/> to run once <paramref name="currentFrame"/> is old enough.</summary>
    public void Enqueue(long currentFrame, Action release) => _pending.Enqueue((currentFrame, release));

    /// <summary>
    /// Runs and removes every entry tagged <paramref name="framesInFlight"/> or more frames
    /// before <paramref name="currentFrame"/>. Entries are enqueued in non-decreasing frame
    /// order (frames only move forward), so peeking the queue head is sufficient, avoiding a
    /// full scan.
    /// </summary>
    public void DrainReady(long currentFrame, int framesInFlight)
    {
        while (_pending.TryPeek(out var entry) && currentFrame - entry.FrameTag >= framesInFlight)
        {
            _pending.Dequeue();
            entry.Release();
        }
    }
}
