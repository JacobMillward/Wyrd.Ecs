namespace Wyrd.Ecs;

public sealed partial class World
{
    /// <summary>
    /// Signals <c>Run</c> to stop, via an ordinary <see cref="Exit"/> event. Sugar for
    /// <c>Emit(new Exit(code))</c> - safe to call from any system, or from outside the tick
    /// loop entirely (a dedicated server's watchdog, a host process's own shutdown signal).
    /// <paramref name="code"/> defaults to <c>0</c> (clean shutdown); see <see cref="Exit.Code"/>.
    /// </summary>
    public void RequestExit(int code = 0) => Emit(new Exit(code));
}
