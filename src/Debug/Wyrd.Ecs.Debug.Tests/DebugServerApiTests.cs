using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Wyrd.Ecs.Debug.Abstractions;
using Wyrd.Ecs.Debug.Internal;

namespace Wyrd.Ecs.Debug.Tests;

public class DebugServerApiTests
{
    // Health's fields are public fields, not properties - JsonSerializer skips fields
    // unless IncludeFields is set, so every test registering it needs this, not the
    // parameterless JsonSerializer.Serialize overload.
    private static readonly JsonSerializerOptions FieldOptions = new() { IncludeFields = true };

    public struct Health : IComponent { public int Current; }

    [Fact]
    public async Task GetSnapshot_WithAConnectedClientAndAnAdvancedTick_ReturnsTheEntitiesAndArchetypes()
    {
        var world = new World();
        var registry = new CodecRegistry();
        registry.Register<Health>("Health",
            h => JsonSerializer.SerializeToUtf8Bytes(h, FieldOptions),
            b => JsonSerializer.Deserialize<Health>(b, FieldOptions));
        world.Commands.CreateEntity(new Health { Current = 7 });
        world.ApplyCommands();
        var (server, port) = DebugServerTestHost.Start(world, registry, p => new DebugServerOptions(Port: p));
        using var _ = server;
        server.Snapshots.Changed += () => { };
        world.AdvanceTick();

        using var client = new HttpClient();
        var response = await client.GetAsync($"http://127.0.0.1:{port}/api/snapshot");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("archetypes")[0].GetProperty("componentDiscriminators")[0].GetString().Should().Be("Health");
        body.GetProperty("entities")[0].GetProperty("components")[0].GetProperty("data").GetProperty("Current").GetInt32().Should().Be(7);

        server.Stop();
    }

    [Fact]
    public async Task GetChangelog_AfterAStructuralChange_ReturnsTheEntry()
    {
        var world = new World();
        var (server, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using var _ = server;
        world.AdvanceTick();
        world.Commands.CreateEntity();
        world.ApplyCommands();

        using var client = new HttpClient();
        var response = await client.GetAsync($"http://127.0.0.1:{port}/api/changelog");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body[0].GetProperty("kind").GetString().Should().Be("EntityCreated");

        server.Stop();
    }

    [Fact]
    public async Task GetEvents_AfterATickAdvance_PushesASnapshotEvent()
    {
        var world = new World();
        var (server, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using var _ = server;
        // Deliberately not subscribing to server.Snapshots.Changed here - opening the
        // SSE connection itself is what should subscribe it, exercised on its own below
        // in GetEvents_OpeningTheConnection_ImplicitlyConnectsTheSnapshotPublisher.

        using var client = new HttpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        string? eventLine;
        string? dataLine;
        string? contentType;
        // Response/stream/reader are disposed here, before server.Stop() - otherwise the
        // SSE connection is still open when Stop() runs, and StopAsync() has to wait out
        // Kestrel's graceful-shutdown grace period for it instead of returning promptly.
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/api/events");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream);

            world.AdvanceTick();

            eventLine = await reader.ReadLineAsync(cts.Token);
            dataLine = await reader.ReadLineAsync(cts.Token);
            contentType = response.Content.Headers.ContentType!.MediaType;
        }

        contentType.Should().Be("text/event-stream");
        eventLine.Should().Be("event: snapshot");
        dataLine.Should().StartWith("data: ");

        server.Stop();
    }

    [Fact]
    public async Task GetEvents_OpeningTheConnection_ImplicitlyConnectsTheSnapshotPublisher()
    {
        var world = new World();
        var (server, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using var _ = server;

        using var client = new HttpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/api/events");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            world.AdvanceTick();

            server.Snapshots.Latest.Should().NotBeNull();
        }

        server.Stop();
    }

    [Fact]
    public async Task GetEvents_AfterTheClientDisconnects_ImplicitlyDisconnectsTheSnapshotPublisher()
    {
        var world = new World();
        var (server, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using var _ = server;

        WorldSnapshot? firstSnapshot;
        using (var client = new HttpClient())
        using (var cts = new CancellationTokenSource())
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/api/events");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream);

            world.AdvanceTick();
            await reader.ReadLineAsync(cts.Token); // "event: snapshot"
            await reader.ReadLineAsync(cts.Token); // "data: ..."
            await reader.ReadLineAsync(cts.Token); // blank line terminating the SSE frame
            firstSnapshot = server.Snapshots.Latest;

            // Cancelling an already-idle connection just lets it go stale - Kestrel
            // only notices via its own heartbeat, on the order of seconds. Cancelling a
            // read that's actually pending forces an immediate abort instead.
            var pendingRead = reader.ReadLineAsync(cts.Token);
            await cts.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pendingRead);
        }
        firstSnapshot.Should().NotBeNull();

        world.Commands.CreateEntity();
        world.ApplyCommands();
        world.AdvanceTick();

        server.Snapshots.Latest.Should().BeSameAs(firstSnapshot);

        server.Stop();
    }

    [Fact]
    public async Task PostFieldEdit_AppliesTheChangeToTheLiveEntity()
    {
        var world = new World();
        var registry = new CodecRegistry();
        registry.Register<Health>("Health",
            h => JsonSerializer.SerializeToUtf8Bytes(h, FieldOptions),
            b => JsonSerializer.Deserialize<Health>(b, FieldOptions));
        Entity entity = world.Commands.CreateEntity(new Health { Current = 3 });
        world.ApplyCommands();
        var (server, port) = DebugServerTestHost.Start(world, registry, p => new DebugServerOptions(Port: p));
        using var _ = server;
        server.Snapshots.Changed += () => { };
        world.AdvanceTick();

        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync(
            $"http://127.0.0.1:{port}/api/entities/{entity.Id}/{entity.Generation}/components/Health",
            new { field = "Current", value = 9 });
        // DecodeInto queues the edit via CommandBuffer.AddComponent - it takes effect on
        // the next ApplyCommands, same as every other structural mutation in this engine.
        world.ApplyCommands();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ref var updated = ref world.TryGetComponent<Health>(entity, out var found);
        found.Should().BeTrue();
        updated.Current.Should().Be(9);

        server.Stop();
    }

    [Fact]
    public async Task PostFieldEdit_ForAnUnknownEntity_ReturnsNotFound()
    {
        var world = new World();
        var (server, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using var _ = server;

        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync(
            $"http://127.0.0.1:{port}/api/entities/999/1/components/Health",
            new { field = "Current", value = 9 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        server.Stop();
    }

    [Fact]
    public async Task PostRendererEdit_AppliesTheCustomRendererAndPersistsTheResult()
    {
        var world = new World();
        var registry = new CodecRegistry();
        registry.Register<Health>("Health",
            h => JsonSerializer.SerializeToUtf8Bytes(h, FieldOptions),
            b => JsonSerializer.Deserialize<Health>(b, FieldOptions));
        Wyrd.Ecs.Debug.DebugRendererRegistry.Register("Health",
            value => new InspectorField.ReadOnly("Current", ((Health)value).Current.ToString()),
            (value, edit) => new Health { Current = edit.AsInt() });
        Entity entity = world.Commands.CreateEntity(new Health { Current = 3 });
        world.ApplyCommands();
        var (server, port) = DebugServerTestHost.Start(world, registry, p => new DebugServerOptions(Port: p));
        using var _ = server;
        server.Snapshots.Changed += () => { };
        world.AdvanceTick();

        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync(
            $"http://127.0.0.1:{port}/api/entities/{entity.Id}/{entity.Generation}/components/Health/renderer-edit",
            9);
        world.ApplyCommands();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ref var updated = ref world.TryGetComponent<Health>(entity, out var found);
        found.Should().BeTrue();
        updated.Current.Should().Be(9);

        server.Stop();
    }

    [Fact]
    public async Task PostRendererEdit_WithAValueTheRendererCannotCoerce_ReturnsBadRequest()
    {
        var world = new World();
        var registry = new CodecRegistry();
        registry.Register<Health>("Health",
            h => JsonSerializer.SerializeToUtf8Bytes(h, FieldOptions),
            b => JsonSerializer.Deserialize<Health>(b, FieldOptions));
        Wyrd.Ecs.Debug.DebugRendererRegistry.Register("Health",
            value => new InspectorField.ReadOnly("Current", ((Health)value).Current.ToString()),
            (value, edit) => new Health { Current = edit.AsInt() });
        Entity entity = world.Commands.CreateEntity(new Health { Current = 3 });
        world.ApplyCommands();
        var (server, port) = DebugServerTestHost.Start(world, registry, p => new DebugServerOptions(Port: p));
        using var _ = server;
        server.Snapshots.Changed += () => { };
        world.AdvanceTick();

        using var client = new HttpClient();
        // AsInt() calls JsonElement.GetInt32(), which throws for a string value.
        var response = await client.PostAsJsonAsync(
            $"http://127.0.0.1:{port}/api/entities/{entity.Id}/{entity.Generation}/components/Health/renderer-edit",
            "not a number");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        server.Stop();
    }

    [Fact]
    public async Task PostPlaybackPause_PausesTheWorld()
    {
        var world = new World();
        var (server, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using var _ = server;

        using var client = new HttpClient();
        var response = await client.PostAsync($"http://127.0.0.1:{port}/api/playback/pause", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        world.IsPaused.Should().BeTrue();

        server.Stop();
    }

    [Fact]
    public async Task PostPlaybackTimescale_SetsTheWorldsTimeScale()
    {
        var world = new World();
        var (server, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using var _ = server;

        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync($"http://127.0.0.1:{port}/api/playback/timescale", new { value = 2.0 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        world.TimeScale.Should().Be(2.0);

        server.Stop();
    }

    [Fact]
    public async Task GetPlayback_ReturnsTheCurrentState()
    {
        var world = new World();
        var (server, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using var _ = server;

        using var client = new HttpClient();
        await client.PostAsync($"http://127.0.0.1:{port}/api/playback/pause", null);
        var response = await client.GetAsync($"http://127.0.0.1:{port}/api/playback");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.GetProperty("isPaused").GetBoolean().Should().BeTrue();

        server.Stop();
    }

    [Fact]
    public async Task GetEvents_AfterATimescaleChange_PushesAPlaybackEvent()
    {
        var world = new World();
        var (server, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using var _ = server;

        using var client = new HttpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        string? eventLine;
        string? dataLine;
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/api/events");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream);

            await client.PostAsJsonAsync($"http://127.0.0.1:{port}/api/playback/timescale", new { value = 2.0 }, cts.Token);

            eventLine = await reader.ReadLineAsync(cts.Token);
            dataLine = await reader.ReadLineAsync(cts.Token);
        }

        eventLine.Should().Be("event: playback");
        dataLine.Should().Be("data: {\"isPaused\":false,\"timeScale\":2}");

        server.Stop();
    }
}
