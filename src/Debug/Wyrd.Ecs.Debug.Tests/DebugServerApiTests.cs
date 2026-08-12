using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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
        server.Snapshots.Connect();
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
        server.Snapshots.Connect();

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
    public async Task GetEvents_AfterTheClientDisconnects_UnsubscribesFromChanged()
    {
        var world = new World();
        var (server, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using var _ = server;

        using (var client = new HttpClient())
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/api/events");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        }

        // Give the server a moment to observe the client's disconnect and run the `finally`.
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        var countBefore = server.ChangeLog.Entries.Count;
        world.Commands.CreateEntity();
        world.ApplyCommands();

        server.ChangeLog.Entries.Count.Should().Be(countBefore + 1);

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
        server.Snapshots.Connect();
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
}
