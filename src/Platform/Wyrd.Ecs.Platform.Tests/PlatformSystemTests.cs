using SDL3;

namespace Wyrd.Ecs.Platform.Tests;

public class PlatformSystemTests
{
    [Fact]
    public void Constructor_InitializesVideoAndCreatesAWindow()
    {
        var world = new WorldBuilder()
            .AddPlatform("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();

        var platform = world.GetSystem<PlatformSystem>();

        platform.Window.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void RemoveSystem_RunsCleanupWithoutThrowing()
    {
        var world = new WorldBuilder()
            .AddPlatform("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var platform = world.GetSystem<PlatformSystem>();

        var act = () => world.RemoveSystem(platform);

        act.Should().NotThrow();
    }

    [Fact]
    public void Update_DrainsPendingEventsIntoEventsBuffer()
    {
        var world = new WorldBuilder()
            .AddPlatform("Test Window", 320, 240, SDL.WindowFlags.Hidden)
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
        // AddPlatform - so this only passes because of [Phase(Phase.PreUpdate)] on
        // PlatformSystem itself, not by accident of registration-order tie-break (which is
        // exactly the "only works because .AddRenderer() is conventionally called last"
        // bug this whole mechanism exists to fix - a same-order test wouldn't catch a
        // regression back to that accidental behavior).
        var builder = new WorldBuilder();
        builder.AddSystemCore(
            typeof(OrdinaryProbeSystem),
            access: null,
            construct: _ => new OrdinaryProbeSystem(),
            generatedBeforeTargets: [],
            generatedAfterTargets: []);
        builder.AddPlatform("Test Window", 320, 240, SDL.WindowFlags.Hidden);
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
            .AddPlatform("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var reader = world.CreateEventReader<DeviceChange>();
        var pushed = expectedKind == DeviceKind.Keyboard
            ? new SDL.Event { Type = (uint)sdlEventType, KDevice = new SDL.KeyboardDeviceEvent { Type = sdlEventType, Which = 77 } }
            : new SDL.Event { Type = (uint)sdlEventType, MDevice = new SDL.MouseDeviceEvent { Type = sdlEventType, Which = 77 } };
        SDL.PushEvent(ref pushed);

        world.Update(TimeSpan.Zero);

        reader.Read().Should().ContainSingle(c => c.DeviceId == 77 && c.DeviceKind == expectedKind && c.Change == expectedChange);
    }

    [Fact]
    public void Update_WithNoHotPlugEvent_EmitsNoDeviceChange()
    {
        var world = new WorldBuilder()
            .AddPlatform("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var reader = world.CreateEventReader<DeviceChange>();

        world.Update(TimeSpan.Zero);

        reader.Read().Should().BeEmpty();
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
