using SDL3;

namespace Wyrd.Ecs.Input;

/// <summary>
/// Mutable, code-first binding data: named actions map to one or more physical inputs.
/// <c>Bind</c>/<c>BindAxis2D</c> are purely additive across calls; use <c>Unbind</c> to
/// remove a binding. A given (profile, action) pair is either digital (<c>Bind</c>) or an
/// axis (<c>BindAxis2D</c>), never both at once - mixing throws, forcing an explicit
/// <c>Unbind</c> first to switch kinds.
/// </summary>
public sealed partial class BindingTable<TAction> where TAction : struct, Enum
{
    internal enum Kind { Digital, Axis2D }

    private readonly Dictionary<(int Profile, TAction Action), Kind> _kinds = [];
    private readonly Dictionary<(int Profile, TAction Action), HashSet<SDL.Scancode>> _keys = [];
    private readonly Dictionary<(int Profile, TAction Action), HashSet<MouseButton>> _mouseButtons = [];
    private readonly Dictionary<(int Profile, TAction Action), (SDL.Scancode Up, SDL.Scancode Down, SDL.Scancode Left, SDL.Scancode Right)> _axes = [];
    private readonly Dictionary<int, HashSet<uint>> _assignedDevices = [];

    private static readonly HashSet<SDL.Scancode> EmptyScancodes = [];
    private static readonly HashSet<MouseButton> EmptyMouseButtons = [];

    /// <summary>Binds <paramref name="keys"/> to <paramref name="action"/> at profile 0, additively. Throws if <paramref name="action"/> is already bound as an axis - call <see cref="Unbind(TAction)"/> first to switch kinds.</summary>
    public BindingTable<TAction> Bind(TAction action, params SDL.Scancode[] keys) => Bind(0, action, keys);

    /// <summary>Same as <see cref="Bind(TAction, SDL.Scancode[])"/>, targeting a specific <paramref name="profile"/> instead of profile 0.</summary>
    public BindingTable<TAction> Bind(int profile, TAction action, params SDL.Scancode[] keys)
    {
        ClaimDigital(profile, action);
        var set = _keys.TryGetValue((profile, action), out var existing) ? existing : _keys[(profile, action)] = [];
        foreach (var key in keys) set.Add(key);
        return this;
    }

    /// <summary>Binds <paramref name="buttons"/> to <paramref name="action"/> at profile 0, additively. Throws if <paramref name="action"/> is already bound as an axis - call <see cref="Unbind(TAction)"/> first to switch kinds.</summary>
    public BindingTable<TAction> Bind(TAction action, params MouseButton[] buttons) => Bind(0, action, buttons);

    /// <summary>Same as <see cref="Bind(TAction, MouseButton[])"/>, targeting a specific <paramref name="profile"/> instead of profile 0.</summary>
    public BindingTable<TAction> Bind(int profile, TAction action, params MouseButton[] buttons)
    {
        ClaimDigital(profile, action);
        var set = _mouseButtons.TryGetValue((profile, action), out var existing) ? existing : _mouseButtons[(profile, action)] = [];
        foreach (var button in buttons) set.Add(button);
        return this;
    }

    /// <summary>Binds a 2D composite (WASD-style) to <paramref name="action"/> at profile 0: each key contributes ±1 on its axis, clamped to unit length. Throws if <paramref name="action"/> is already bound digitally - call <see cref="Unbind(TAction)"/> first to switch kinds.</summary>
    public BindingTable<TAction> BindAxis2D(TAction action, SDL.Scancode up, SDL.Scancode down, SDL.Scancode left, SDL.Scancode right) =>
        BindAxis2D(0, action, up, down, left, right);

    /// <summary>Same as <see cref="BindAxis2D(TAction, SDL.Scancode, SDL.Scancode, SDL.Scancode, SDL.Scancode)"/>, targeting a specific <paramref name="profile"/> instead of profile 0.</summary>
    public BindingTable<TAction> BindAxis2D(int profile, TAction action, SDL.Scancode up, SDL.Scancode down, SDL.Scancode left, SDL.Scancode right)
    {
        if (_kinds.TryGetValue((profile, action), out var kind) && kind == Kind.Digital)
            throw new InvalidOperationException(
                $"Action '{action}' at profile {profile} is already bound as a digital action. Call Unbind() first to switch it to an axis.");
        _kinds[(profile, action)] = Kind.Axis2D;
        _axes[(profile, action)] = (up, down, left, right);
        return this;
    }

    private void ClaimDigital(int profile, TAction action)
    {
        if (_kinds.TryGetValue((profile, action), out var kind) && kind == Kind.Axis2D)
            throw new InvalidOperationException(
                $"Action '{action}' at profile {profile} is already bound as an axis. Call Unbind() first to switch it to a digital action.");
        _kinds[(profile, action)] = Kind.Digital;
    }

    /// <summary>Clears every physical binding for <paramref name="action"/> at profile 0 (both digital and axis).</summary>
    public BindingTable<TAction> Unbind(TAction action) => Unbind(0, action);

    /// <summary>Same as <see cref="Unbind(TAction)"/>, targeting a specific <paramref name="profile"/> instead of profile 0.</summary>
    public BindingTable<TAction> Unbind(int profile, TAction action)
    {
        _kinds.Remove((profile, action));
        _keys.Remove((profile, action));
        _mouseButtons.Remove((profile, action));
        _axes.Remove((profile, action));
        return this;
    }

    /// <summary>Removes just <paramref name="key"/> from <paramref name="action"/>'s bindings at profile 0, leaving any other bindings intact.</summary>
    public BindingTable<TAction> Unbind(TAction action, SDL.Scancode key) => Unbind(0, action, key);

    /// <summary>Same as <see cref="Unbind(TAction, SDL.Scancode)"/>, targeting a specific <paramref name="profile"/> instead of profile 0.</summary>
    public BindingTable<TAction> Unbind(int profile, TAction action, SDL.Scancode key)
    {
        if (_keys.TryGetValue((profile, action), out var set)) set.Remove(key);
        return this;
    }

    /// <summary>Removes just <paramref name="button"/> from <paramref name="action"/>'s bindings at profile 0, leaving any other bindings intact.</summary>
    public BindingTable<TAction> Unbind(TAction action, MouseButton button) => Unbind(0, action, button);

    /// <summary>Same as <see cref="Unbind(TAction, MouseButton)"/>, targeting a specific <paramref name="profile"/> instead of profile 0.</summary>
    public BindingTable<TAction> Unbind(int profile, TAction action, MouseButton button)
    {
        if (_mouseButtons.TryGetValue((profile, action), out var set)) set.Remove(button);
        return this;
    }

    /// <summary>Adds <paramref name="deviceId"/> to <paramref name="profile"/>'s assigned devices, additively - call again with a different id to also track a mouse alongside a keyboard, for example. An unassigned profile merges every connected device of the relevant kind instead.</summary>
    public BindingTable<TAction> AssignDevice(int profile, uint deviceId)
    {
        var set = _assignedDevices.TryGetValue(profile, out var existing) ? existing : _assignedDevices[profile] = [];
        set.Add(deviceId);
        return this;
    }

    /// <summary>Clears every device assigned to <paramref name="profile"/>, reverting it to merging every connected device.</summary>
    public BindingTable<TAction> UnassignDevice(int profile)
    {
        _assignedDevices.Remove(profile);
        return this;
    }

    /// <summary>Removes <paramref name="deviceId"/> from whichever profile(s) it was assigned to, without affecting any other device assigned to those profiles. A profile left with no assigned devices reverts to unassigned (merging every connected device), not an empty explicit assignment.</summary>
    internal void UnassignDeviceById(uint deviceId)
    {
        foreach (var (profile, set) in _assignedDevices.ToList())
        {
            set.Remove(deviceId);
            if (set.Count == 0) _assignedDevices.Remove(profile);
        }
    }

    /// <summary>Every (profile, action) pair with a live binding, and which kind it is.</summary>
    internal IEnumerable<(int Profile, TAction Action, Kind Kind)> BoundActions() =>
        _kinds.Select(kv => (kv.Key.Profile, kv.Key.Action, kv.Value));

    /// <summary>The keys bound to <paramref name="action"/> at <paramref name="profile"/>, or an empty set if none.</summary>
    internal IReadOnlySet<SDL.Scancode> KeysFor(int profile, TAction action) =>
        _keys.TryGetValue((profile, action), out var set) ? set : EmptyScancodes;

    /// <summary>The mouse buttons bound to <paramref name="action"/> at <paramref name="profile"/>, or an empty set if none.</summary>
    internal IReadOnlySet<MouseButton> MouseButtonsFor(int profile, TAction action) =>
        _mouseButtons.TryGetValue((profile, action), out var set) ? set : EmptyMouseButtons;

    /// <summary>The axis composite bound to <paramref name="action"/> at <paramref name="profile"/>, or <c>null</c> if none.</summary>
    internal (SDL.Scancode Up, SDL.Scancode Down, SDL.Scancode Left, SDL.Scancode Right)? AxisFor(int profile, TAction action) =>
        _axes.TryGetValue((profile, action), out var axis) ? axis : null;

    /// <summary>The device ids explicitly assigned to <paramref name="profile"/>, or <c>null</c> if unassigned (meaning "merge every connected device").</summary>
    internal IReadOnlySet<uint>? AssignedDevicesFor(int profile) =>
        _assignedDevices.TryGetValue(profile, out var set) ? set : null;
}
