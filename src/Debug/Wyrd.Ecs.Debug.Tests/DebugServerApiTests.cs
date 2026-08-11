using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

namespace Wyrd.Ecs.Debug.Tests;

public class DebugServerApiTests
{
    // Health's fields are public fields, not properties - JsonSerializer skips fields
    // unless IncludeFields is set, so every test registering it needs this, not the
    // parameterless JsonSerializer.Serialize overload.
    private static readonly JsonSerializerOptions FieldOptions = new() { IncludeFields = true };

    private static int FreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public struct Health : IComponent { public int Current; }

    [Fact]
    public async Task GetSnapshot_WithAConnectedClientAndAnAdvancedTick_ReturnsTheEntitiesAndArchetypes()
    {
        var port = FreeLoopbackPort();
        var world = new World();
        var registry = new CodecRegistry();
        registry.Register<Health>("Health",
            h => JsonSerializer.SerializeToUtf8Bytes(h, FieldOptions),
            b => JsonSerializer.Deserialize<Health>(b, FieldOptions));
        world.Commands.CreateEntity(new Health { Current = 7 });
        world.ApplyCommands();
        using var server = new DebugServer(world, registry, new DebugServerOptions(Port: port));
        server.Start();
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
        var port = FreeLoopbackPort();
        var world = new World();
        using var server = new DebugServer(world, new CodecRegistry(), new DebugServerOptions(Port: port));
        server.Start();
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
        var port = FreeLoopbackPort();
        var world = new World();
        using var server = new DebugServer(world, new CodecRegistry(), new DebugServerOptions(Port: port));
        server.Start();
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
        var port = FreeLoopbackPort();
        var world = new World();
        using var server = new DebugServer(world, new CodecRegistry(), new DebugServerOptions(Port: port));
        server.Start();

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
}
