namespace Wyrd.Ecs.Persistence.Continuous.Internal;

/// <summary>
/// A process-wide safety net for continuous persistence sessions whose caller never
/// called <c>World.StopContinuousPersistence</c>. Registers one
/// <see cref="AppDomain.ProcessExit"/> handler, lazily, the first time any session opts
/// in, and force-stops every still-tracked session when it fires. This is a net for
/// "the process exited and cleanup code never ran" (a crash, a forced quit, a missing
/// try/finally) — not a substitute for calling Stop, and not a fix for a World
/// abandoned mid-process while the game keeps running: that still leaks its two threads
/// until the process itself exits, since World isn't IDisposable and nothing here can
/// make it so.
/// </summary>
internal static class ProcessExitSafetyNet
{
    private static readonly object Lock = new();
    private static readonly Dictionary<World, Action<bool>> TrackedStops = new();
    private static bool _handlerRegistered;

    /// <summary>Tracks <paramref name="world"/>'s session for the process-exit sweep. <paramref name="stop"/> is invoked with <c>true</c> if the sweep fires.</summary>
    internal static void Register(World world, Action<bool> stop)
    {
        lock (Lock)
        {
            TrackedStops[world] = stop;
            if (!_handlerRegistered)
            {
                AppDomain.CurrentDomain.ProcessExit += (_, _) => StopAllTrackedSessions();
                _handlerRegistered = true;
            }
        }
    }

    /// <summary>Stops tracking <paramref name="world"/>'s session — called from StopContinuousPersistence regardless of whether that session opted in, so a manually-stopped session is never swept twice.</summary>
    internal static void Unregister(World world)
    {
        lock (Lock) TrackedStops.Remove(world);
    }

    /// <summary>The actual sweep, extracted so it's testable without firing a real ProcessExit event.</summary>
    internal static void StopAllTrackedSessions()
    {
        List<Action<bool>> stops;
        lock (Lock)
        {
            stops = [.. TrackedStops.Values];
            TrackedStops.Clear();
        }

        foreach (var stop in stops)
        {
            try { stop(true); }
            catch
            {
                // Best-effort: each session's own onError callback already saw anything
                // reportable. Nothing is left to hand this to at process exit.
            }
        }
    }
}
