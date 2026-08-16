using System.Net;

namespace Wyrd.Ecs.Debug.Tests;

public class DebugServerStaticFilesTests
{
    [Fact]
    public async Task GetRoot_ServesIndexHtml()
    {
        var world = new World();
        var (server, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using var _ = server;

        using var client = new HttpClient();
        var response = await client.GetAsync($"http://127.0.0.1:{port}/");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("<div id=\"app\"></div>");

        server.Stop();
    }

    [Fact]
    public async Task GetAppJs_ServesTheBuiltBundle()
    {
        var world = new World();
        var (server, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using var _ = server;

        using var client = new HttpClient();
        var response = await client.GetAsync($"http://127.0.0.1:{port}/js/app.js");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain("Wyrd.Ecs Debug");

        server.Stop();
    }
}
