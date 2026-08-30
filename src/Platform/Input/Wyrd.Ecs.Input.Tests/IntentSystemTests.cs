using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Input.Tests;

public class IntentSystemTests
{
    private static (World World, PlatformSystem Platform, BindingTable<TestAction> Bindings) BuildWorld()
    {
        var bindings = new BindingTable<TestAction>();
        var world = new WorldBuilder()
            .AddWindow("Test Window", 320, 240, SDL.WindowFlags.Hidden)
            .Build();
        var platform = world.GetSystem<PlatformSystem>()!;
        world.AddSystemCore(
            typeof(IntentSystem<TestAction>),
            access: null,
            construct: w => new IntentSystem<TestAction>(w, platform, bindings),
            generatedBeforeTargets: [],
            generatedAfterTargets: []);
        return (world, platform, bindings);
    }

    [Fact]
    public void Constructor_RegistersIntentStateAsAResource()
    {
        var (world, _, _) = BuildWorld();

        var act = () => world.GetResource<IntentState<TestAction>>();

        act.Should().NotThrow();
    }

    [Fact]
    public void KeyDownEvent_MakesTheBoundActionHeldAndJustPressed()
    {
        var (world, _, bindings) = BuildWorld();
        bindings.Bind(TestAction.Jump, SDL.Scancode.Space);
        var down = new SDL.Event { Type = (uint)SDL.EventType.KeyDown, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyDown, Scancode = SDL.Scancode.Space, Down = true } };
        SDL.PushEvent(ref down);

        world.Update(TimeSpan.Zero);

        var state = world.GetResource<IntentState<TestAction>>();
        state[TestAction.Jump].IsHeld.Should().BeTrue();
        state[TestAction.Jump].JustPressed.Should().BeTrue();
    }

    [Fact]
    public void KeyHeldAcrossTwoTicks_JustPressedIsOnlyTrueOnTheFirst()
    {
        var (world, _, bindings) = BuildWorld();
        bindings.Bind(TestAction.Jump, SDL.Scancode.Space);
        var down = new SDL.Event { Type = (uint)SDL.EventType.KeyDown, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyDown, Scancode = SDL.Scancode.Space, Down = true } };
        SDL.PushEvent(ref down);
        world.Update(TimeSpan.Zero);

        world.Update(TimeSpan.Zero); // no new event pushed; key stays held per SDL's own down-state tracking

        var state = world.GetResource<IntentState<TestAction>>();
        state[TestAction.Jump].IsHeld.Should().BeTrue();
        state[TestAction.Jump].JustPressed.Should().BeFalse();
    }

    [Fact]
    public void KeyUpEvent_ClearsHeldAndSetsJustReleased()
    {
        var (world, _, bindings) = BuildWorld();
        bindings.Bind(TestAction.Jump, SDL.Scancode.Space);
        var down = new SDL.Event { Type = (uint)SDL.EventType.KeyDown, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyDown, Scancode = SDL.Scancode.Space, Down = true } };
        SDL.PushEvent(ref down);
        world.Update(TimeSpan.Zero);
        var up = new SDL.Event { Type = (uint)SDL.EventType.KeyUp, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyUp, Scancode = SDL.Scancode.Space, Down = false } };
        SDL.PushEvent(ref up);

        world.Update(TimeSpan.Zero);

        var state = world.GetResource<IntentState<TestAction>>();
        state[TestAction.Jump].IsHeld.Should().BeFalse();
        state[TestAction.Jump].JustReleased.Should().BeTrue();
    }

    [Fact]
    public void KeyDownEvent_MakesTheBoundActionTickJustPressedToo()
    {
        var (world, _, bindings) = BuildWorld();
        bindings.Bind(TestAction.Jump, SDL.Scancode.Space);
        var down = new SDL.Event { Type = (uint)SDL.EventType.KeyDown, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyDown, Scancode = SDL.Scancode.Space, Down = true } };
        SDL.PushEvent(ref down);

        world.Update(TimeSpan.Zero);

        world.GetResource<IntentState<TestAction>>()[TestAction.Jump].TickJustPressed.Should().BeTrue();
    }

    [Fact]
    public void KeyHeldAcrossManyTicksWithNoConsumer_TickJustPressedStaysTrue()
    {
        var (world, _, bindings) = BuildWorld();
        bindings.Bind(TestAction.Jump, SDL.Scancode.Space);
        var down = new SDL.Event { Type = (uint)SDL.EventType.KeyDown, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyDown, Scancode = SDL.Scancode.Space, Down = true } };
        SDL.PushEvent(ref down);
        world.Update(TimeSpan.Zero);

        for (var i = 0; i < 5; i++) world.Update(TimeSpan.Zero); // nothing clears it; simulates several real calls with no fixed step

        world.GetResource<IntentState<TestAction>>()[TestAction.Jump].TickJustPressed.Should().BeTrue("nothing has consumed/cleared it yet");
    }

    [Fact]
    public void PressThenReleaseBeforeAnyConsumerReads_BothTickFlagsAreSet()
    {
        var (world, _, bindings) = BuildWorld();
        bindings.Bind(TestAction.Jump, SDL.Scancode.Space);
        var down = new SDL.Event { Type = (uint)SDL.EventType.KeyDown, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyDown, Scancode = SDL.Scancode.Space, Down = true } };
        SDL.PushEvent(ref down);
        world.Update(TimeSpan.Zero);
        var up = new SDL.Event { Type = (uint)SDL.EventType.KeyUp, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyUp, Scancode = SDL.Scancode.Space, Down = false } };
        SDL.PushEvent(ref up);

        world.Update(TimeSpan.Zero);

        var state = world.GetResource<IntentState<TestAction>>()[TestAction.Jump];
        state.TickJustPressed.Should().BeTrue("net accumulated state, not a full sub-frame transition log: a documented, accepted limitation");
        state.TickJustReleased.Should().BeTrue();
    }

    [Fact]
    public void UnbindingAnAction_RemovesItsStateEntry()
    {
        var (world, _, bindings) = BuildWorld();
        bindings.Bind(TestAction.Jump, SDL.Scancode.Space);
        world.Update(TimeSpan.Zero);
        world.GetResource<IntentState<TestAction>>().TryGet(TestAction.Jump, out _).Should().BeTrue();

        bindings.Unbind(TestAction.Jump);
        world.Update(TimeSpan.Zero);

        world.GetResource<IntentState<TestAction>>().TryGet(TestAction.Jump, out _).Should().BeFalse();
    }

    [Fact]
    public void BindAxis2DAction_WasdKeysProduceANormalizedVector()
    {
        var (world, _, bindings) = BuildWorld();
        bindings.BindAxis2D(TestAction.Move, SDL.Scancode.W, SDL.Scancode.S, SDL.Scancode.A, SDL.Scancode.D);
        var right = new SDL.Event { Type = (uint)SDL.EventType.KeyDown, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyDown, Scancode = SDL.Scancode.D, Down = true } };
        var up = new SDL.Event { Type = (uint)SDL.EventType.KeyDown, Key = new SDL.KeyboardEvent { Type = SDL.EventType.KeyDown, Scancode = SDL.Scancode.W, Down = true } };
        SDL.PushEvent(ref right);
        SDL.PushEvent(ref up);

        world.Update(TimeSpan.Zero);

        var value = world.GetResource<IntentState<TestAction>>()[TestAction.Move].Value;
        value.Length().Should().BeApproximately(1f, 0.0001f);
        value.X.Should().BeGreaterThan(0f);
        value.Y.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void MouseMotionEvent_UpdatesPositionAndAccumulatesDelta()
    {
        var (world, _, _) = BuildWorld();
        var motion = new SDL.Event { Type = (uint)SDL.EventType.MouseMotion, Motion = new SDL.MouseMotionEvent { Type = SDL.EventType.MouseMotion, X = 10, Y = 20, XRel = 3, YRel = 4 } };
        SDL.PushEvent(ref motion);

        world.Update(TimeSpan.Zero);

        var state = world.GetResource<IntentState<TestAction>>();
        state.MousePosition.Should().Be(new System.Numerics.Vector2(10, 20));
        state.MouseDelta.Should().Be(new System.Numerics.Vector2(3, 4));
    }

    [Fact]
    public void MouseDelta_ResetsToZeroEachTickEvenWithNoNewMotionEvent()
    {
        var (world, _, _) = BuildWorld();
        var motion = new SDL.Event { Type = (uint)SDL.EventType.MouseMotion, Motion = new SDL.MouseMotionEvent { Type = SDL.EventType.MouseMotion, X = 10, Y = 20, XRel = 3, YRel = 4 } };
        SDL.PushEvent(ref motion);
        world.Update(TimeSpan.Zero);

        world.Update(TimeSpan.Zero);

        world.GetResource<IntentState<TestAction>>().MouseDelta.Should().Be(System.Numerics.Vector2.Zero);
    }
}
