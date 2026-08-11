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
}
