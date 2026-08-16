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
    public async Task WithDebugServer_ZeroArgOverload_WithAPort_UsesIt()
    {
        var port = FreeLoopbackPort();
        var world = new World();

        using var server = world.WithDebugServer(port);

        using var probe = new TcpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await probe.ConnectAsync(IPAddress.Loopback, port, cts.Token);
    }
}
