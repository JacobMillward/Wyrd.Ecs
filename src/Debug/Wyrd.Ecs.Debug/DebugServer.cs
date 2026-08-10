using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Wyrd.Ecs.Debug;

/// <summary>
/// Owns the in-process host lifecycle for the debug/inspection server: bind/listen/stop
/// against <c>127.0.0.1</c> only, the security boundary for this server.
/// <see cref="Start"/> lets a bind failure (e.g. the port is already in
/// use) throw normally; <see cref="World.WithDebugServer"/> is the sugar layer that
/// catches that and routes it through <see cref="DebugServerOptions.OnError"/> instead.
/// </summary>
public sealed class DebugServer : IDisposable
{
    private readonly World _world;
    private readonly DebugServerOptions _options;
    private WebApplication? _app;

    public DebugServer(World world, DebugServerOptions options)
    {
        _world = world;
        _options = options;
    }

    /// <summary>Binds and starts listening. Throws if the port is already in use.</summary>
    public void Start()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{_options.Port}");
        _app = builder.Build();
        _app.Start();
    }

    /// <summary>Stops listening and releases the port. No-op if not started.</summary>
    public void Stop()
    {
        _app?.StopAsync().GetAwaiter().GetResult();
        _app?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _app = null;
    }

    public void Dispose() => Stop();
}
