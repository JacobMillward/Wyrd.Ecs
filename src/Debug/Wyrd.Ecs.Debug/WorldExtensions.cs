namespace Wyrd.Ecs.Debug;

public static class WorldExtensions
{
    /// <summary>
    /// Constructs, starts, and returns a <see cref="DebugServer"/> for <paramref name="world"/>.
    /// A bind failure is caught and routed through <paramref name="options"/>'s
    /// <see cref="DebugServerOptions.OnError"/> (a bare <see cref="Console.Error"/> write if
    /// none was supplied) rather than thrown, since a debug tool failing to bind a port
    /// shouldn't take down the game. Caller owns the returned <see cref="DebugServer"/>'s lifetime and
    /// must dispose it before <paramref name="world"/> is torn down.
    /// </summary>
    public static DebugServer WithDebugServer(this World world, CodecRegistry registry, DebugServerOptions? options = null)
    {
        options ??= new DebugServerOptions();
        var server = new DebugServer(world, options);
        try
        {
            server.Start();
        }
        catch (Exception ex)
        {
            var onError = options.OnError ?? (e => Console.Error.WriteLine(e));
            onError(ex);
        }

        return server;
    }
}
