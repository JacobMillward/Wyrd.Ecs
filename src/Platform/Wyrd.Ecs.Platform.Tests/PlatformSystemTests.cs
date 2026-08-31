using SDL3;

namespace Wyrd.Ecs.Platform.Tests;

public class PlatformSystemTests
{
    [Fact]
    public void Constructor_InitializesVideoAndCreatesAWindow()
    {
        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();

        var platform = world.GetSystem<PlatformSystem>();

        platform.Window.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void RemoveSystem_RunsCleanupWithoutThrowing()
    {
        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var platform = world.GetSystem<PlatformSystem>();

        var act = () => world.RemoveSystem(platform);

        act.Should().NotThrow();
    }

    [Fact]
    public void Update_DrainsPendingEventsIntoEventsBuffer()
    {
        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var pushed = new SDL.Event { Type = (uint)SDL.EventType.Quit };
        SDL.PushEvent(ref pushed);

        world.Update(TimeSpan.Zero);

        var platform = world.GetSystem<PlatformSystem>();
        platform.Events.Should().Contain(e => e.Type == (uint)SDL.EventType.Quit);
    }

    [Fact]
    public void PlatformSystem_HasAlreadyPumpedTheEventBeforeAnOrdinarySystemsFirstTick()
    {
        // AddSystemCore, not the generated AddSystem<T>() sugar: this test project
        // doesn't reference Wyrd.Ecs.Generators as an analyzer (no QuerySystem/query-chain
        // code lives here), so that sugar doesn't exist in this compilation.
        //
        // Registered deliberately in the adversarial order - OrdinaryProbeSystem BEFORE
        // AddWindow - so this only passes because AddWindow() applies Phase.PreUpdate
        // fluently (via SystemRegistration.Phase(), not a class attribute), not by accident
        // of registration-order tie-break (which is exactly the "only works because
        // .AddRenderer() is conventionally called last" bug this whole mechanism exists to
        // fix - a same-order test wouldn't catch a regression back to that accidental
        // behavior).
        var builder = new WorldBuilder();
        builder.AddSystemCore(
            typeof(OrdinaryProbeSystem),
            access: null,
            construct: _ => new OrdinaryProbeSystem(),
            generatedBeforeTargets: [],
            generatedAfterTargets: []);
        builder.AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden);
        var world = builder.Build();
        var pushed = new SDL.Event { Type = (uint)SDL.EventType.Quit };
        SDL.PushEvent(ref pushed);

        world.Update(TimeSpan.Zero);

        // Not an exact Events.Count: window creation alone generates several real native
        // SDL events (window-shown, exposed, etc.) besides the one pushed here, so an exact
        // count is noise, not signal. What actually proves ordering is whether the pushed
        // Quit event specifically was already visible - if PlatformSystem hadn't pumped yet
        // when OrdinaryProbeSystem's first Execute ran, Events would still be last tick's
        // (empty, since this is tick 1), not contain this tick's freshly-pumped event.
        world.GetSystem<OrdinaryProbeSystem>()!.SawQuitEventOnFirstTick.Should().BeTrue();
    }

    [Theory]
    [InlineData(SDL.EventType.KeyboardAdded, DeviceKind.Keyboard, DeviceChangeKind.Connected)]
    [InlineData(SDL.EventType.KeyboardRemoved, DeviceKind.Keyboard, DeviceChangeKind.Disconnected)]
    [InlineData(SDL.EventType.MouseAdded, DeviceKind.Mouse, DeviceChangeKind.Connected)]
    [InlineData(SDL.EventType.MouseRemoved, DeviceKind.Mouse, DeviceChangeKind.Disconnected)]
    public void Update_EmitsADeviceChangeEventForHotPlugSdlEvents(SDL.EventType sdlEventType, DeviceKind expectedKind, DeviceChangeKind expectedChange)
    {
        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var reader = world.CreateEventReader<DeviceChange>();
        var pushed = expectedKind == DeviceKind.Keyboard
            ? new SDL.Event { Type = (uint)sdlEventType, KDevice = new SDL.KeyboardDeviceEvent { Type = sdlEventType, Which = 77 } }
            : new SDL.Event { Type = (uint)sdlEventType, MDevice = new SDL.MouseDeviceEvent { Type = sdlEventType, Which = 77 } };
        SDL.PushEvent(ref pushed);

        world.Update(TimeSpan.Zero);

        reader.Read().Should().ContainSingle(c => c.DeviceId == new DeviceId(77) && c.DeviceKind == expectedKind && c.Change == expectedChange);
    }

    [Fact]
    public void Update_OnSdlQuitEvent_RequestsExit()
    {
        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var reader = world.CreateEventReader<Exit>();
        var pushed = new SDL.Event { Type = (uint)SDL.EventType.Quit };
        SDL.PushEvent(ref pushed);

        world.Update(TimeSpan.Zero);

        reader.Read().Should().ContainSingle(e => e.Code == 0);
    }

    [Theory]
    [InlineData(SDL.EventType.AudioDeviceAdded, DeviceChangeKind.Connected)]
    [InlineData(SDL.EventType.AudioDeviceRemoved, DeviceChangeKind.Disconnected)]
    public void Update_EmitsADeviceChangeEventForAudioOutputHotPlugSdlEvents(SDL.EventType sdlEventType, DeviceChangeKind expectedChange)
    {
        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var reader = world.CreateEventReader<DeviceChange>();
        var pushed = new SDL.Event { Type = (uint)sdlEventType, ADevice = new SDL.AudioDeviceEvent { Type = sdlEventType, Which = 77, Recording = false } };
        SDL.PushEvent(ref pushed);

        world.Update(TimeSpan.Zero);

        reader.Read().Should().ContainSingle(c => c.DeviceId == new DeviceId(77) && c.DeviceKind == DeviceKind.AudioOutput && c.Change == expectedChange);
    }

    [Fact]
    public void Update_IgnoresRecordingDeviceHotPlugEvents()
    {
        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var reader = world.CreateEventReader<DeviceChange>();
        var pushed = new SDL.Event { Type = (uint)SDL.EventType.AudioDeviceAdded, ADevice = new SDL.AudioDeviceEvent { Type = SDL.EventType.AudioDeviceAdded, Which = 77, Recording = true } };
        SDL.PushEvent(ref pushed);

        world.Update(TimeSpan.Zero);

        reader.Read().Should().BeEmpty();
    }

    [Fact]
    public void Update_WithNoHotPlugEvent_EmitsNoDeviceChange()
    {
        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var reader = world.CreateEventReader<DeviceChange>();

        world.Update(TimeSpan.Zero);

        reader.Read().Should().BeEmpty();
    }

    [Fact]
    public void Constructor_SeedsConnectedDevicesFromTheLiveSdlSnapshot()
    {
        var expected = (SDL.GetKeyboards(out _) ?? []).Select(id => new DeviceId(id))
            .Concat((SDL.GetMice(out _) ?? []).Select(id => new DeviceId(id)));

        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();

        world.GetResource<ConnectedDevices>().DevicesById.Keys.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData(SDL.EventType.KeyboardAdded, DeviceKind.Keyboard)]
    [InlineData(SDL.EventType.MouseAdded, DeviceKind.Mouse)]
    public void Update_OnDeviceAdded_AddsItToConnectedDevices(SDL.EventType sdlEventType, DeviceKind expectedKind)
    {
        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var pushed = expectedKind == DeviceKind.Keyboard
            ? new SDL.Event { Type = (uint)sdlEventType, KDevice = new SDL.KeyboardDeviceEvent { Type = sdlEventType, Which = 77 } }
            : new SDL.Event { Type = (uint)sdlEventType, MDevice = new SDL.MouseDeviceEvent { Type = sdlEventType, Which = 77 } };
        SDL.PushEvent(ref pushed);

        world.Update(TimeSpan.Zero);

        world.GetResource<ConnectedDevices>().DevicesById.Should().Contain(new DeviceId(77), expectedKind);
    }

    [Theory]
    [InlineData(SDL.EventType.KeyboardRemoved, DeviceKind.Keyboard)]
    [InlineData(SDL.EventType.MouseRemoved, DeviceKind.Mouse)]
    public void Update_OnDeviceRemoved_RemovesItFromConnectedDevices(SDL.EventType sdlEventType, DeviceKind expectedKind)
    {
        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var added = expectedKind == DeviceKind.Keyboard
            ? new SDL.Event { Type = (uint)SDL.EventType.KeyboardAdded, KDevice = new SDL.KeyboardDeviceEvent { Type = SDL.EventType.KeyboardAdded, Which = 77 } }
            : new SDL.Event { Type = (uint)SDL.EventType.MouseAdded, MDevice = new SDL.MouseDeviceEvent { Type = SDL.EventType.MouseAdded, Which = 77 } };
        SDL.PushEvent(ref added);
        world.Update(TimeSpan.Zero);
        var removed = expectedKind == DeviceKind.Keyboard
            ? new SDL.Event { Type = (uint)sdlEventType, KDevice = new SDL.KeyboardDeviceEvent { Type = sdlEventType, Which = 77 } }
            : new SDL.Event { Type = (uint)sdlEventType, MDevice = new SDL.MouseDeviceEvent { Type = sdlEventType, Which = 77 } };
        SDL.PushEvent(ref removed);

        world.Update(TimeSpan.Zero);

        world.GetResource<ConnectedDevices>().DevicesById.Should().NotContainKey(new DeviceId(77));
    }
}

file sealed class OrdinaryProbeSystem : EcsSystem
{
    private bool _observed;

    public bool SawQuitEventOnFirstTick { get; private set; }

    protected override void Execute(World world, Time time)
    {
        if (_observed) return;
        _observed = true;
        SawQuitEventOnFirstTick = world.GetSystem<PlatformSystem>()!.Events.Any(e => e.Type == (uint)SDL.EventType.Quit);
    }
}
