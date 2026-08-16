using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Wyrd.Ecs.Debug.Tests;

public class EndToEndApiSmokeTests
{
    public struct Health : IComponent { public int Current; }

    // Health's fields are public fields, not properties - JsonSerializer skips fields
    // unless IncludeFields is set.
    private static readonly JsonSerializerOptions FieldOptions = new() { IncludeFields = true };

    [Fact]
    public async Task ASnapshotFetchAnSseConnectionAndAFieldEdit_AllWorkTogetherAgainstOneRunningServer()
    {
        var world = new World();
        var registry = new CodecRegistry();
        registry.Register<Health>("Health",
            h => JsonSerializer.SerializeToUtf8Bytes(h, FieldOptions),
            b => JsonSerializer.Deserialize<Health>(b, FieldOptions));
        Entity entity = world.Commands.CreateEntity(new Health { Current = 1 });
        world.ApplyCommands();
        var (server, port) = DebugServerTestHost.Start(world, registry, p => new DebugServerOptions(Port: p));
        using var _ = server;
        server.Snapshots.Changed += () => { };
        world.AdvanceTick();

        using var client = new HttpClient();

        // 1. Initial snapshot fetch.
        var snapshotResponse = await client.GetAsync($"http://127.0.0.1:{port}/api/snapshot");
        snapshotResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Open the live event stream.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        string? eventLine;
        string? dataLine;
        // Response/stream/reader are disposed in this inner scope, before server.Stop() -
        // otherwise the SSE connection is still open when Stop() runs, and StopAsync()
        // has to wait out Kestrel's graceful-shutdown grace period for it.
        {
            var eventsRequest = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/api/events");
            using var eventsResponse = await client.SendAsync(eventsRequest, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            using var stream = await eventsResponse.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream);

            // 3. Edit a field. DecodeInto only queues the change - ApplyCommands is what
            // actually applies it, same as every other structural mutation in this engine.
            var editResponse = await client.PostAsJsonAsync(
                $"http://127.0.0.1:{port}/api/entities/{entity.Id}/{entity.Generation}/components/Health",
                new { field = "Current", value = 42 });
            world.ApplyCommands();
            editResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Advancing the tick pushes the edited state over the already-open stream.
            world.AdvanceTick();
            eventLine = await reader.ReadLineAsync(cts.Token);
            dataLine = await reader.ReadLineAsync(cts.Token);
        }

        eventLine.Should().Be("event: snapshot");
        // Data's casing is whatever Health's own codec encoded (PascalCase field names
        // here), not the outer camelCase policy - see EncodedComponentJsonConverterTests.
        dataLine.Should().Contain("\"Current\":42");

        // 5. Playback controls also work against the same running server.
        var pauseResponse = await client.PostAsync($"http://127.0.0.1:{port}/api/playback/pause", null);
        pauseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        world.IsPaused.Should().BeTrue();

        server.Stop();
    }
}
