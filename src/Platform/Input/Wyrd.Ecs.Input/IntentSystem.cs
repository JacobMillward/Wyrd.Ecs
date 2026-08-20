using System.Numerics;
using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Input;

/// <summary>
/// Drains <see cref="PlatformSystem"/>'s pumped SDL events into a per-tick
/// <c>IntentState&lt;TAction&gt;</c> resource, resolving <see cref="Bindings"/>' current
/// contents fresh every tick - a hand-written <see cref="EcsSystem"/>, not a
/// <see cref="QuerySystem"/>, since it must run exactly once per tick regardless of
/// entity count. Carries no <c>[Phase]</c>/<c>[RunBefore]</c>/<c>[RunAfter]</c> itself - a
/// generic EcsSystem can't be discovered by the query-chain generator at all (it emits a
/// registry entry keyed by the class's own open type parameter, which doesn't compile) -
/// <c>WorldBuilderInputExtensions.AddInput</c> applies the
/// <see cref="Phase.PreUpdate"/>/<see cref="PlatformSystem"/> ordering via
/// <c>SystemRegistration.Phase()</c>/<c>.After&lt;T&gt;()</c> instead, at the closed-generic
/// call site.
/// </summary>
public sealed partial class IntentSystem<TAction> : EcsSystem where TAction : struct, Enum
{
    private readonly PlatformSystem _platform;
    private readonly Dictionary<(TAction Action, int Seat), bool> _previousHeld = [];

    /// <summary>The live binding table this system resolves every tick - mutate it (or its overrides) and the change applies on the very next tick.</summary>
    public BindingTable<TAction> Bindings { get; }

    /// <summary>Registers this system's <c>IntentState&lt;TAction&gt;</c> resource on <paramref name="world"/> immediately.</summary>
    public IntentSystem(World world, PlatformSystem platform, BindingTable<TAction> bindings)
    {
        _platform = platform;
        Bindings = bindings;
        world.AddResource(new IntentState<TAction>());
    }

    /// <inheritdoc/>
    protected override void Execute(World world, Time time)
    {
        ref var state = ref world.GetResourceRef<IntentState<TAction>>();
        state.MouseDelta = Vector2.Zero;
        state.WheelDelta = Vector2.Zero;
        state.ConnectedThisTickList.Clear();
        state.DisconnectedThisTickList.Clear();

        foreach (var ev in _platform.Events)
            HandleEvent(ev, ref state);

        state.States.Clear();
        foreach (var (seat, action, kind) in Bindings.BoundActions())
        {
            var value = kind == BindingTable<TAction>.Kind.Axis2D
                ? ResolveAxis(seat, action)
                : (ResolveDigital(seat, action) ? Vector2.UnitX : Vector2.Zero);
            var isHeld = value != Vector2.Zero;
            var wasHeld = _previousHeld.GetValueOrDefault((action, seat));
            state.States[(action, seat)] = new ActionState(isHeld, isHeld && !wasHeld, !isHeld && wasHeld, value);
            _previousHeld[(action, seat)] = isHeld;
        }
    }

    private Vector2 ResolveAxis(int seat, TAction action)
    {
        if (Bindings.AxisFor(seat, action) is not { } axis) return Vector2.Zero;
        var x = (KeyIsDown(seat, axis.Right) ? 1f : 0f) - (KeyIsDown(seat, axis.Left) ? 1f : 0f);
        var y = (KeyIsDown(seat, axis.Up) ? 1f : 0f) - (KeyIsDown(seat, axis.Down) ? 1f : 0f);
        var v = new Vector2(x, y);
        return v == Vector2.Zero ? v : Vector2.Normalize(v);
    }

    private bool ResolveDigital(int seat, TAction action)
    {
        foreach (var key in Bindings.KeysFor(seat, action))
            if (KeyIsDown(seat, key)) return true;
        foreach (var button in Bindings.MouseButtonsFor(seat, action))
            if (MouseButtonIsDown(seat, button)) return true;
        return false;
    }

    // Single merged device source (Task 8 scope): every seat reads the same global
    // down-state, since no seat can be explicitly assigned to a device yet. Task 9
    // replaces this block with a per-device version in IntentSystem.Devices.cs.
    private readonly HashSet<SDL.Scancode> _keysDown = [];
    private readonly HashSet<MouseButton> _mouseButtonsDown = [];

    private void HandleEvent(SDL.Event ev, ref IntentState<TAction> state)
    {
        switch ((SDL.EventType)ev.Type)
        {
            case SDL.EventType.KeyDown:
                _keysDown.Add(ev.Key.Scancode);
                break;
            case SDL.EventType.KeyUp:
                _keysDown.Remove(ev.Key.Scancode);
                break;
            case SDL.EventType.MouseButtonDown:
                if (MouseButtonExtensions.FromSdlButton(ev.Button.Button) is { } downButton) _mouseButtonsDown.Add(downButton);
                break;
            case SDL.EventType.MouseButtonUp:
                if (MouseButtonExtensions.FromSdlButton(ev.Button.Button) is { } upButton) _mouseButtonsDown.Remove(upButton);
                break;
            case SDL.EventType.MouseMotion:
                state.MousePosition = new Vector2(ev.Motion.X, ev.Motion.Y);
                state.MouseDelta += new Vector2(ev.Motion.XRel, ev.Motion.YRel);
                break;
            case SDL.EventType.MouseWheel:
                state.WheelDelta += new Vector2(ev.Wheel.X, ev.Wheel.Y);
                break;
        }
    }

    private bool KeyIsDown(int seat, SDL.Scancode key) => _keysDown.Contains(key);

    private bool MouseButtonIsDown(int seat, MouseButton button) => _mouseButtonsDown.Contains(button);
}
