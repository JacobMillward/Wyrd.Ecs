using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wyrd.Ecs.Debug.Internal;

namespace Wyrd.Ecs.Debug;

/// <summary>
/// Owns the in-process host lifecycle for the debug/inspection server: bind/listen/stop
/// against <c>127.0.0.1</c> only, the security boundary for this server.
/// <see cref="Start"/> lets a bind failure (e.g. the port is already in
/// use) throw normally; <see cref="World.WithDebugServer"/> is the generated layer that
/// catches that and routes it through <see cref="DebugServerOptions.OnError"/> instead.
/// </summary>
public sealed class DebugServer : IDisposable
{
    private readonly World _world;
    private readonly DebugServerOptions _options;
    private readonly SnapshotPublisher _snapshots;
    private readonly ChangeLogRecorder _changeLog;
    private readonly PlaybackControls _playback;
    private WebApplication? _app;
    private IDisposable? _structuralChangeHandle;

    internal SnapshotPublisher Snapshots => _snapshots;
    internal ChangeLogRecorder ChangeLog => _changeLog;
    internal PlaybackControls Playback => _playback;

    public DebugServer(World world, CodecRegistry registry, DebugServerOptions options)
    {
        _world = world;
        _options = options;
        _snapshots = new SnapshotPublisher(world, registry);
        _changeLog = new ChangeLogRecorder(options.ChangeLogCapacity);
        _playback = new PlaybackControls(world);
    }

    /// <summary>Binds and starts listening. Throws if the port is already in use.</summary>
    public void Start()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.Converters.Add(new EncodedComponentJsonConverter());
            // Enums (ChangeKind) default to numeric values without this - a debug UI
            // wants "EntityCreated", not "0".
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        _app = builder.Build();

        _app.MapGet("/api/snapshot", () => Results.Json(_snapshots.Latest, statusCode: _snapshots.Latest is null ? 404 : 200));
        _app.MapGet("/api/changelog", () => Results.Json(_changeLog.Entries));

        _app.Urls.Add($"http://127.0.0.1:{_options.Port}");
        _app.Start();

        _world.OnTickAdvanced += _snapshots.OnTickAdvanced;
        _world.OnTickAdvanced += _changeLog.AdvanceTick;
        _structuralChangeHandle = _world.ObserveStructuralChanges(_changeLog);
    }

    /// <summary>Stops listening, releases the port, and unsubscribes from the world. No-op if not started.</summary>
    public void Stop()
    {
        _world.OnTickAdvanced -= _snapshots.OnTickAdvanced;
        _world.OnTickAdvanced -= _changeLog.AdvanceTick;
        _structuralChangeHandle?.Dispose();
        _structuralChangeHandle = null;

        _app?.StopAsync().GetAwaiter().GetResult();
        _app?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _app = null;
    }

    public void Dispose() => Stop();
}
