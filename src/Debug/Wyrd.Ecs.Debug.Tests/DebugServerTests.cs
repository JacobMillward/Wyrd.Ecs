using System.Net;
using System.Net.Sockets;

namespace Wyrd.Ecs.Debug.Tests;

public class DebugServerTests
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
    public void Start_BindsTheConfiguredLoopbackPort()
    {
        var port = FreeLoopbackPort();
        var world = new World();
        using var server = new DebugServer(world, new CodecRegistry(), new DebugServerOptions(Port: port));

        server.Start();

        using var probe = new TcpClient();
        var connected = probe.ConnectAsync(IPAddress.Loopback, port).Wait(TimeSpan.FromSeconds(5));
        connected.Should().BeTrue();

        server.Stop();
    }

    [Fact]
    public void Stop_ReleasesThePortForANewServerToBindAgain()
    {
        var port = FreeLoopbackPort();
        var world = new World();

        using (var first = new DebugServer(world, new CodecRegistry(), new DebugServerOptions(Port: port)))
        {
            first.Start();
            first.Stop();
        }

        using var second = new DebugServer(world, new CodecRegistry(), new DebugServerOptions(Port: port));
        var act = () => second.Start();
        act.Should().NotThrow();
        second.Stop();
    }

    [Fact]
    public void Start_OnAPortAlreadyInUse_Throws()
    {
        var port = FreeLoopbackPort();
        var world = new World();
        using var blocker = new DebugServer(world, new CodecRegistry(), new DebugServerOptions(Port: port));
        blocker.Start();

        using var contender = new DebugServer(world, new CodecRegistry(), new DebugServerOptions(Port: port));
        var act = () => contender.Start();

        act.Should().Throw<IOException>();

        blocker.Stop();
    }

    [Fact]
    public void AfterStop_OnTickAdvancedNoLongerPublishesSnapshots()
    {
        var port = FreeLoopbackPort();
        var world = new World();
        var registry = new CodecRegistry();
        var server = new DebugServer(world, registry, new DebugServerOptions(Port: port));
        server.Start();
        server.Snapshots.Connect();
        world.AdvanceTick();
        var firstSnapshot = server.Snapshots.Latest;

        server.Stop();
        world.AdvanceTick();

        server.Snapshots.Latest.Should().BeSameAs(firstSnapshot);
    }

    [Fact]
    public void AfterDispose_AStructuralChangeProducesNoNewLogEntry()
    {
        var port = FreeLoopbackPort();
        var world = new World();
        var registry = new CodecRegistry();
        var server = new DebugServer(world, registry, new DebugServerOptions(Port: port));
        server.Start();
        var countBeforeDispose = server.ChangeLog.Entries.Count;

        server.Dispose();
        world.Commands.CreateEntity();
        world.ApplyCommands();

        server.ChangeLog.Entries.Should().HaveCount(countBeforeDispose);
    }

    [Fact]
    public void AfterStart_AStructuralChangeIsStampedWithTheCurrentTick()
    {
        var port = FreeLoopbackPort();
        var world = new World();
        var registry = new CodecRegistry();
        using var server = new DebugServer(world, registry, new DebugServerOptions(Port: port));
        server.Start();

        world.AdvanceTick();
        world.AdvanceTick();
        world.Commands.CreateEntity();
        world.ApplyCommands();

        server.ChangeLog.Entries[0].Tick.Should().Be(world.CurrentTick);
    }
}
