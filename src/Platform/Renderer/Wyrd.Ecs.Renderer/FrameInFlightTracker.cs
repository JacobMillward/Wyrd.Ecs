namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Tracks which of <see cref="FramesInFlight"/> rotating slots the current frame owns.
/// Shared by the per-frame instance buffer (later phases) and <see cref="DeferredDestroyQueue"/>,
/// since both need the same counter to stay in sync.
/// </summary>
public sealed class FrameInFlightTracker
{
    /// <summary>Number of frames that may be in flight on the GPU at once.</summary>
    public const int FramesInFlight = 3;

    /// <summary>Monotonically increasing count of frames submitted so far.</summary>
    public long CurrentFrame { get; private set; }

    /// <summary><see cref="CurrentFrame"/> modulo <see cref="FramesInFlight"/>.</summary>
    public int SlotIndex => (int)(CurrentFrame % FramesInFlight);

    /// <summary>Called once per submitted frame, after <c>SDL_SubmitGPUCommandBuffer</c>.</summary>
    public void Advance() => CurrentFrame++;
}
