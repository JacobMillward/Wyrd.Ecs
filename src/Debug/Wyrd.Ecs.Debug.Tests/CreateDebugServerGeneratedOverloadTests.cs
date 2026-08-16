using System.Net;
using System.Net.Sockets;

namespace Wyrd.Ecs.Debug.Tests;

public class CreateDebugServerGeneratedOverloadTests
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
    public void CreateDebugServer_DoesNotStartListening()
    {
        var port = FreeLoopbackPort();
        var world = new World();

        using var server = world.CreateDebugServer(new DebugServerOptions(Port: port));

        // A synchronous Connect, not ConnectAsync().Wait(timeout): nothing listening means
        // the OS refuses the connection almost immediately, which Wait() would re-throw as
        // an AggregateException rather than returning false - Connect's own throw is what
        // we actually want to assert on.
        using var probe = new TcpClient();
        var act = () => probe.Connect(IPAddress.Loopback, port);
        act.Should().Throw<SocketException>();
    }

    [Fact]
    public async Task CreateDebugServer_ThenStart_ListensWithNoRegistryEverSuppliedByTheCaller()
    {
        var port = FreeLoopbackPort();
        var world = new World();
        using var server = world.CreateDebugServer(new DebugServerOptions(Port: port));

        server.Start();

        using var probe = new TcpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await probe.ConnectAsync(IPAddress.Loopback, port, cts.Token);
    }
}
