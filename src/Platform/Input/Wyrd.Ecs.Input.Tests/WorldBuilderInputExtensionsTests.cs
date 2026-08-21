using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Input.Tests;

public class WorldBuilderInputExtensionsTests
{
    [Fact]
    public void AddInput_WiresUpASystemThatRespondsToARealEventThroughTheFullBuilderChain()
    {
        var bindings = new BindingTable<TestAction>().Bind(TestAction.Jump, SDL.Scancode.Space);
        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .AddInput(bindings)
            .Build();
        var down = new SDL.Event { Type = (uint)SDL.EventType.KeyDown, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyDown, Scancode = SDL.Scancode.Space, Down = true } };
        SDL.PushEvent(ref down);

        world.Update(TimeSpan.Zero);

        world.GetResource<IntentState<TestAction>>()[TestAction.Jump].IsHeld.Should().BeTrue();
    }

    [Fact]
    public void AddInput_CalledBeforeAddWindow_StillRespondsToARealEvent()
    {
        var bindings = new BindingTable<TestAction>().Bind(TestAction.Jump, SDL.Scancode.Space);
        var world = new WorldBuilder()
            .AddInput(bindings)
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var down = new SDL.Event { Type = (uint)SDL.EventType.KeyDown, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyDown, Scancode = SDL.Scancode.Space, Down = true } };
        SDL.PushEvent(ref down);

        world.Update(TimeSpan.Zero);

        world.GetResource<IntentState<TestAction>>()[TestAction.Jump].IsHeld.Should().BeTrue();
    }

    [Fact]
    public void AddInput_WithNoAddWindowInTheChain_ThrowsNamingPlatformSystem()
    {
        var bindings = new BindingTable<TestAction>().Bind(TestAction.Jump, SDL.Scancode.Space);
        var builder = new WorldBuilder().AddInput(bindings);

        var act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>().WithMessage("*IntentSystem*PlatformSystem*");
    }
}
