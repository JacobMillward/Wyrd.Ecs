using System.Net;
using System.Net.Sockets;

namespace Wyrd.Ecs.Debug.Tests;

public class DebugServerTests
{
    [Fact]
    public void Start_BindsTheConfiguredLoopbackPort()
    {
        var world = new World();
        var (server, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using var _ = server;

        using var probe = new TcpClient();
        var connected = probe.ConnectAsync(IPAddress.Loopback, port).Wait(TimeSpan.FromSeconds(5));
        connected.Should().BeTrue();

        server.Stop();
    }

    [Fact]
    public void Stop_ReleasesThePortForANewServerToBindAgain()
    {
        var world = new World();

        var (first, port) = DebugServerTestHost.Start(world, new CodecRegistry(), p => new DebugServerOptions(Port: p));
        using (first)
        {
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
        // Deliberately not DebugServerTestHost.Start: this test needs a real,
        // deterministic port conflict, not a helper that retries past one.
        var port = DebugServerTestHost.FreeLoopbackPort();
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
        var world = new World();
        var registry = new CodecRegistry();
        var (server, _) = DebugServerTestHost.Start(world, registry, p => new DebugServerOptions(Port: p));
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
        var world = new World();
        var registry = new CodecRegistry();
        var (server, _) = DebugServerTestHost.Start(world, registry, p => new DebugServerOptions(Port: p));
        var countBeforeDispose = server.ChangeLog.Entries.Count;

        server.Dispose();
        world.Commands.CreateEntity();
        world.ApplyCommands();

        server.ChangeLog.Entries.Should().HaveCount(countBeforeDispose);
    }

    [Fact]
    public void AfterStart_AStructuralChangeIsStampedWithTheCurrentTick()
    {
        var world = new World();
        var registry = new CodecRegistry();
        var (server, _) = DebugServerTestHost.Start(world, registry, p => new DebugServerOptions(Port: p));
        using var _ = server;

        world.AdvanceTick();
        world.AdvanceTick();
        world.Commands.CreateEntity();
        world.ApplyCommands();

        server.ChangeLog.Entries[0].Tick.Should().Be(world.CurrentTick);
    }
}
