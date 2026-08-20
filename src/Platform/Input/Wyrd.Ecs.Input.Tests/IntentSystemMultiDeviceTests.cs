using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Input.Tests;

public class IntentSystemMultiDeviceTests
{
    private static (World World, BindingTable<TestAction> Bindings) BuildWorld()
    {
        var bindings = new BindingTable<TestAction>();
        var world = new WorldBuilder()
            .AddPlatform("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var platform = world.GetSystem<PlatformSystem>()!;
        world.AddSystemCore(
            typeof(IntentSystem<TestAction>),
            access: null,
            construct: w => new IntentSystem<TestAction>(w, platform, bindings),
            generatedBeforeTargets: [],
            generatedAfterTargets: []);
        return (world, bindings);
    }

    private static SDL.Event KeyboardAdded(uint deviceId) =>
        new() { Type = (uint)SDL.EventType.KeyboardAdded, KDevice = new SDL.KeyboardDeviceEvent { Type = SDL.EventType.KeyboardAdded, Which = deviceId } };

    private static SDL.Event KeyboardRemoved(uint deviceId) =>
        new() { Type = (uint)SDL.EventType.KeyboardRemoved, KDevice = new SDL.KeyboardDeviceEvent { Type = SDL.EventType.KeyboardRemoved, Which = deviceId } };

    private static SDL.Event KeyDown(SDL.Scancode key, uint deviceId) =>
        new() { Type = (uint)SDL.EventType.KeyDown, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyDown, Scancode = key, Down = true, Which = deviceId } };

    [Fact]
    public void KeyboardAddedEvent_IsObservableViaTheSharedDeviceChangeEventChannel()
    {
        // DeviceChange is emitted by PlatformSystem (the single canonical source, see its
        // own doc comment) - IntentSystem only consumes it for its own down-state
        // bookkeeping. This confirms that consumption doesn't somehow swallow the event
        // for anyone else subscribed to the same channel.
        var (world, _) = BuildWorld();
        var reader = world.CreateEventReader<DeviceChange>();
        var added = KeyboardAdded(42);
        SDL.PushEvent(ref added);

        world.Update(TimeSpan.Zero);

        reader.Read().Should().ContainSingle(c => c.DeviceId == 42 && c.DeviceKind == DeviceKind.Keyboard && c.Change == DeviceChangeKind.Connected);
    }

    [Fact]
    public void AssignedSeat_OnlyRespondsToItsOwnDevicesKeyPresses()
    {
        var (world, bindings) = BuildWorld();
        bindings.Bind(seat: 0, TestAction.Jump, SDL.Scancode.Space);
        bindings.Bind(seat: 1, TestAction.Jump, SDL.Scancode.Space);
        bindings.AssignDevice(0, 111u);
        bindings.AssignDevice(1, 222u);
        var press = KeyDown(SDL.Scancode.Space, deviceId: 111u);
        SDL.PushEvent(ref press);

        world.Update(TimeSpan.Zero);

        var state = world.GetResource<IntentState<TestAction>>();
        state[TestAction.Jump, seat: 0].IsHeld.Should().BeTrue();
        state[TestAction.Jump, seat: 1].IsHeld.Should().BeFalse();
    }

    [Fact]
    public void UnassignedSeat_MergesEveryDevice()
    {
        var (world, bindings) = BuildWorld();
        bindings.Bind(TestAction.Jump, SDL.Scancode.Space); // seat 0, never assigned a device
        var press = KeyDown(SDL.Scancode.Space, deviceId: 999u);
        SDL.PushEvent(ref press);

        world.Update(TimeSpan.Zero);

        world.GetResource<IntentState<TestAction>>()[TestAction.Jump].IsHeld.Should().BeTrue();
    }

    [Fact]
    public void DeviceRemovedMidPress_ForceClearsThatDevicesDownStateSameTick()
    {
        var (world, bindings) = BuildWorld();
        var deviceChanges = world.CreateEventReader<DeviceChange>();
        bindings.Bind(TestAction.Jump, SDL.Scancode.Space);
        bindings.AssignDevice(0, 111u);
        var press = KeyDown(SDL.Scancode.Space, deviceId: 111u);
        SDL.PushEvent(ref press);
        world.Update(TimeSpan.Zero);
        world.GetResource<IntentState<TestAction>>()[TestAction.Jump].IsHeld.Should().BeTrue("pre-condition: the press registered");
        var removed = KeyboardRemoved(111u);
        SDL.PushEvent(ref removed);

        world.Update(TimeSpan.Zero);

        var state = world.GetResource<IntentState<TestAction>>();
        state[TestAction.Jump].IsHeld.Should().BeFalse("a disconnect must never leave an action stuck held");
        state[TestAction.Jump].JustReleased.Should().BeTrue();
        deviceChanges.Read().Should().Contain(c => c.DeviceId == 111u && c.DeviceKind == DeviceKind.Keyboard && c.Change == DeviceChangeKind.Disconnected);
    }

    [Fact]
    public void DeviceRemoved_AlsoClearsItsSeatAssignment()
    {
        var (world, bindings) = BuildWorld();
        bindings.AssignDevice(0, 111u);
        var removed = KeyboardRemoved(111u);
        SDL.PushEvent(ref removed);

        world.Update(TimeSpan.Zero);

        bindings.AssignedDevicesFor(0).Should().BeNull();
    }
}
