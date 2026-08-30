using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Input.Tests;

public class WorldBuilderInputExtensionsTests
{
    [Fact]
    public void AddInput_RegistersATickResetSystemThatActuallyClearsTheTickPairAfterAFixedStep()
    {
        var bindings = new BindingTable<TestAction>();
        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .WithFixedTimestep(TimeSpan.FromSeconds(1.0 / 60.0))
            .AddInput(bindings)
            .Build();
        bindings.Bind(TestAction.Jump, SDL.Scancode.Space);
        var down = new SDL.Event { Type = (uint)SDL.EventType.KeyDown, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyDown, Scancode = SDL.Scancode.Space, Down = true } };
        SDL.PushEvent(ref down);
        world.Update(TimeSpan.Zero); // IntentSystem's Variable pass processes the event this call. Fixed runs before Variable within a call, so nothing has consumed it yet.
        world.GetResource<IntentState<TestAction>>()[TestAction.Jump].TickJustPressed
            .Should().BeTrue("set by IntentSystem, not yet consumed by any fixed step");

        world.Update(TimeSpan.FromSeconds(1.0)); // this call's fixed loop runs before its own Variable pass, and should observe and clear the pending edge

        world.GetResource<IntentState<TestAction>>()[TestAction.Jump].TickJustPressed
            .Should().BeFalse("IntentTickResetSystem should have cleared it once a fixed step consumed it");
    }
}
