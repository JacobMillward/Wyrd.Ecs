using System.Net;
using System.Net.Sockets;

namespace Wyrd.Ecs.Debug.Tests;

public class WithDebugServerGeneratedOverloadTests
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
    public void WithDebugServer_ZeroArgOverload_WithAPort_UsesIt()
    {
        var port = FreeLoopbackPort();
        var world = new World();

        using var server = world.WithDebugServer(port);

        using var probe = new TcpClient();
        var connected = probe.ConnectAsync(IPAddress.Loopback, port).Wait(TimeSpan.FromSeconds(5));
        connected.Should().BeTrue();
    }
}
