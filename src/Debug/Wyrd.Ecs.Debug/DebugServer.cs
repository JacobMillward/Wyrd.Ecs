using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Wyrd.Ecs.Debug.Abstractions;
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
    private readonly CodecRegistry _registry;
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
        _registry = registry;
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

        _app.MapGet("/api/events", async (HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            // Setting headers alone doesn't send anything - Kestrel buffers until the
            // first body write. A caller awaiting response headers (before any event has
            // fired) would otherwise hang until the first Changed. The explicit flush
            // matters too: StartAsync alone commits the headers without necessarily
            // pushing them onto the wire yet.
            await context.Response.StartAsync(cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);

            var channel = Channel.CreateUnbounded<string>();
            var jsonOptions = context.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

            void OnSnapshotChanged() => channel.Writer.TryWrite(FormatSseEvent("snapshot", _snapshots.Latest, jsonOptions));
            void OnChangeLogChanged() => channel.Writer.TryWrite(FormatSseEvent("changelog", _changeLog.Entries, jsonOptions));

            _snapshots.Changed += OnSnapshotChanged;
            _changeLog.Changed += OnChangeLogChanged;

            try
            {
                await foreach (var message in channel.Reader.ReadAllAsync(cancellationToken))
                {
                    await context.Response.WriteAsync(message, cancellationToken);
                    await context.Response.Body.FlushAsync(cancellationToken);
                }
            }
            finally
            {
                _snapshots.Changed -= OnSnapshotChanged;
                _changeLog.Changed -= OnChangeLogChanged;
            }
        });

        _app.MapPost("/api/entities/{id:int}/{generation:int}/components/{discriminator}", (
            int id, int generation, string discriminator, FieldEditRequest request) =>
        {
            var entity = new Entity(id, generation);
            if (_snapshots.Latest is not { } snapshot) return Results.NotFound();

            var entitySnapshot = snapshot.Entities.FirstOrDefault(e => e.Entity == entity);
            if (entitySnapshot.Entity.IsNull) return Results.NotFound();

            var component = entitySnapshot.Components.FirstOrDefault(c => c.Discriminator == discriminator);
            if (component.Discriminator is null) return Results.NotFound();

            if (!_registry.TryGetByDebugName(discriminator, out var codec)) return Results.NotFound();

            try
            {
                var merged = FieldMerge.MergeField(component.Data, request.Field, request.Value);
                codec.DecodeInto(_world, entity, merged);
                return Results.Ok();
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        _app.MapPost("/api/entities/{id:int}/{generation:int}/components/{discriminator}/renderer-edit", (
            int id, int generation, string discriminator, JsonElement editValue) =>
        {
            var entity = new Entity(id, generation);
            if (_snapshots.Latest is not { } snapshot) return Results.NotFound();

            var entitySnapshot = snapshot.Entities.FirstOrDefault(e => e.Entity == entity);
            if (entitySnapshot.Entity.IsNull) return Results.NotFound();

            var component = entitySnapshot.Components.FirstOrDefault(c => c.Discriminator == discriminator);
            if (component.Discriminator is null) return Results.NotFound();

            if (!_registry.TryGetByDebugName(discriminator, out var codec)) return Results.NotFound();
            if (!DebugRendererRegistry.TryGetRenderer(discriminator, out var renderer)) return Results.NotFound();

            var currentValue = codec.DecodeValue(component.Data);
            var newValue = renderer.Apply(currentValue, new InspectorEdit(editValue));
            codec.DecodeInto(_world, entity, codec.EncodeValue(newValue));
            return Results.Ok();
        });

        _app.MapPost("/api/playback/pause", () => { _playback.Pause(); return Results.Ok(); });
        _app.MapPost("/api/playback/resume", () => { _playback.Resume(); return Results.Ok(); });
        _app.MapPost("/api/playback/timescale", (SetTimeScaleRequest request) =>
        {
            _playback.SetTimeScale(request.Value);
            return Results.Ok();
        });

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

    private static string FormatSseEvent(string eventName, object? payload, JsonSerializerOptions options) =>
        $"event: {eventName}\ndata: {JsonSerializer.Serialize(payload, options)}\n\n";
}
