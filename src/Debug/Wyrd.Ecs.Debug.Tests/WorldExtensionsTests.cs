using System.Net;
using System.Net.Sockets;

namespace Wyrd.Ecs.Debug.Tests;

public class WorldExtensionsTests
{
    private static int FreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public void WithDebugServer_ReturnsAStartedServer()
    {
        var port = FreeLoopbackPort();
        var world = new World();
        var registry = new CodecRegistry();

        using var server = world.WithDebugServer(registry, new DebugServerOptions(Port: port));

        using var probe = new TcpClient();
        var connected = probe.ConnectAsync(IPAddress.Loopback, port).Wait(TimeSpan.FromSeconds(5));
        connected.Should().BeTrue();
    }

    [Fact]
    public void WithDebugServer_OnBindFailure_RoutesThroughOnErrorInsteadOfThrowing()
    {
        var port = FreeLoopbackPort();
        var world = new World();
        var registry = new CodecRegistry();
        using var blocker = world.WithDebugServer(registry, new DebugServerOptions(Port: port));

        Exception? reported = null;
        var act = () => world.WithDebugServer(registry, new DebugServerOptions(Port: port, OnError: ex => reported = ex));

        act.Should().NotThrow();
        reported.Should().NotBeNull();
    }

    [Fact]
    public void WithDebugServer_NoOptions_UsesTheDefaultPort()
    {
        var world = new World();
        var registry = new CodecRegistry();

        using var server = world.WithDebugServer(registry);

        using var probe = new TcpClient();
        var connected = probe.ConnectAsync(IPAddress.Loopback, 5299).Wait(TimeSpan.FromSeconds(5));
        connected.Should().BeTrue();
    }
}
