using System.Diagnostics;

namespace Wyrd.Ecs;

public sealed partial class World
{
    /// <summary>
    /// Signals <see cref="Run"/> to stop, via an ordinary <see cref="Exit"/> event. Sugar for
    /// <c>Emit(new Exit(code))</c> - safe to call from any system, or from outside the tick
    /// loop entirely (a dedicated server's watchdog, a host process's own shutdown signal).
    /// <paramref name="code"/> defaults to <c>0</c> (clean shutdown); see <see cref="Exit.Code"/>.
    /// </summary>
    public void RequestExit(int code = 0) => Emit(new Exit(code));

    /// <summary>
    /// Blocks the calling thread, calling <see cref="Update"/> once per iteration with the real
    /// elapsed time since the last call, until an <see cref="Exit"/> event is observed. Purely
    /// additive: <see cref="Update"/> stays public and unaffected, for a consumer embedding its
    /// own loop (a host application, a test harness). <paramref name="targetFrameTime"/> is
    /// <c>null</c> by default - a windowed app relies on the renderer's swapchain vsync to bound
    /// the loop, as it already does today. A headless caller with nothing else bounding
    /// iteration rate passes e.g. <c>TimeSpan.FromSeconds(1.0 / 60)</c> to sleep-pace instead of
    /// spinning a core at 100%.
    /// </summary>
    public void Run(TimeSpan? targetFrameTime = null)
    {
        var exitReader = CreateEventReader<Exit>();
        var clock = Stopwatch.StartNew();
        var last = clock.Elapsed;
        while (true)
        {
            var elapsed = clock.Elapsed;
            Update(elapsed - last);
            last = elapsed;
            if (exitReader.Read().Count > 0) return;
            if (targetFrameTime is { } target)
            {
                var remaining = target - (clock.Elapsed - elapsed);
                if (remaining > TimeSpan.Zero) Thread.Sleep(remaining);
            }
        }
    }
}
