using System.Net;
using System.Net.Sockets;

namespace Wyrd.Ecs.Debug.Tests;

/// <summary>
/// Starts a <see cref="DebugServer"/> on a free loopback port, retrying with a newly
/// picked port if the one this process chose loses a race to another test's server
/// binding first. Probing a <see cref="TcpListener"/> for a free port, releasing it, then
/// binding <see cref="DebugServer"/> to that same port moments later has an inherent gap -
/// under solution-wide parallel test execution, another test can grab the same port in
/// that window. <see cref="DebugServerTests.Start_OnAPortAlreadyInUse_Throws"/> is the one
/// test that must NOT go through this retry, since it specifically verifies a deterministic
/// conflict.
/// </summary>
internal static class DebugServerTestHost
{
    public static (DebugServer Server, int Port) Start(World world, CodecRegistry registry, Func<int, DebugServerOptions> options)
    {
        for (var attempt = 1; ; attempt++)
        {
            var port = FreeLoopbackPort();
            var server = new DebugServer(world, registry, options(port));
            try
            {
                server.Start();
                return (server, port);
            }
            catch (IOException)
            {
                server.Dispose();
                if (attempt >= 10) throw;
            }
        }
    }

    public static int FreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
