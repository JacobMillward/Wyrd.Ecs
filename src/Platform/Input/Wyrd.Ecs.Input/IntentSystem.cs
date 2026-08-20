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
    private readonly EventReader<DeviceChange> _deviceChanges;
    private readonly Dictionary<(TAction Action, int Profile), bool> _previousHeld = [];

    /// <summary>The live binding table this system resolves every tick - mutate it (or its overrides) and the change applies on the very next tick.</summary>
    public BindingTable<TAction> Bindings { get; }

    /// <summary>Registers this system's <c>IntentState&lt;TAction&gt;</c> resource on <paramref name="world"/> immediately.</summary>
    public IntentSystem(World world, PlatformSystem platform, BindingTable<TAction> bindings)
    {
        _platform = platform;
        _deviceChanges = world.CreateEventReader<DeviceChange>();
        Bindings = bindings;
        world.AddResource(new IntentState<TAction>());
    }

    /// <inheritdoc/>
    protected override void Execute(World world, Time time)
    {
        ref var state = ref world.GetResourceRef<IntentState<TAction>>();
        state.MouseDelta = Vector2.Zero;
        state.WheelDelta = Vector2.Zero;

        foreach (var ev in _platform.Events)
            HandleEvent(ev, ref state);

        ApplyDeviceChanges();

        state.States.Clear();
        foreach (var (profile, action, kind) in Bindings.BoundActions())
        {
            var value = kind == BindingTable<TAction>.Kind.Axis2D
                ? ResolveAxis(profile, action)
                : (ResolveDigital(profile, action) ? Vector2.UnitX : Vector2.Zero);
            var isHeld = value != Vector2.Zero;
            var wasHeld = _previousHeld.GetValueOrDefault((action, profile));
            state.States[(action, profile)] = new ActionState(isHeld, isHeld && !wasHeld, !isHeld && wasHeld, value);
            _previousHeld[(action, profile)] = isHeld;
        }
    }

    private Vector2 ResolveAxis(int profile, TAction action)
    {
        if (Bindings.AxisFor(profile, action) is not { } axis) return Vector2.Zero;
        var x = (KeyIsDown(profile, axis.Right) ? 1f : 0f) - (KeyIsDown(profile, axis.Left) ? 1f : 0f);
        var y = (KeyIsDown(profile, axis.Up) ? 1f : 0f) - (KeyIsDown(profile, axis.Down) ? 1f : 0f);
        var v = new Vector2(x, y);
        return v == Vector2.Zero ? v : Vector2.Normalize(v);
    }

    private bool ResolveDigital(int profile, TAction action)
    {
        foreach (var key in Bindings.KeysFor(profile, action))
            if (KeyIsDown(profile, key)) return true;
        foreach (var button in Bindings.MouseButtonsFor(profile, action))
            if (MouseButtonIsDown(profile, button)) return true;
        return false;
    }
}
