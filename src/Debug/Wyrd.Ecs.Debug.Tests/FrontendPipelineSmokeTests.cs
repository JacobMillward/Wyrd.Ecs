using System.Net;

namespace Wyrd.Ecs.Debug.Tests;

public class FrontendPipelineSmokeTests
{
    [Fact]
    public async Task ARunningServer_ServesAPageThatReferencesTheBuiltBundle()
    {
        var world = new World();
        var (server, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using var _ = server;

        using var client = new HttpClient();

        var htmlResponse = await client.GetAsync($"http://127.0.0.1:{port}/");
        var html = await htmlResponse.Content.ReadAsStringAsync();
        htmlResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("/js/app.js");
        html.Should().Contain("/css/app.css");

        var jsResponse = await client.GetAsync($"http://127.0.0.1:{port}/js/app.js");
        var js = await jsResponse.Content.ReadAsStringAsync();
        jsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        js.Should().Contain("EventSource");
        js.Should().Contain("/api/events");

        var cssResponse = await client.GetAsync($"http://127.0.0.1:{port}/css/app.css");
        cssResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        server.Stop();
    }
}
