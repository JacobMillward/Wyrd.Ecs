using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Input;

public sealed partial class IntentSystem<TAction>
{
    private readonly Dictionary<uint, HashSet<SDL.Scancode>> _keysDownByDevice = [];
    private readonly Dictionary<uint, HashSet<MouseButton>> _mouseButtonsDownByDevice = [];

    private void HandleEvent(SDL.Event ev, ref IntentState<TAction> state)
    {
        switch ((SDL.EventType)ev.Type)
        {
            case SDL.EventType.KeyDown:
                DeviceSet(_keysDownByDevice, ev.Key.Which).Add(ev.Key.Scancode);
                break;
            case SDL.EventType.KeyUp:
                DeviceSet(_keysDownByDevice, ev.Key.Which).Remove(ev.Key.Scancode);
                break;
            case SDL.EventType.MouseButtonDown:
                if (MouseButtonExtensions.FromSdlButton(ev.Button.Button) is { } downButton)
                    DeviceSet(_mouseButtonsDownByDevice, ev.Button.Which).Add(downButton);
                break;
            case SDL.EventType.MouseButtonUp:
                if (MouseButtonExtensions.FromSdlButton(ev.Button.Button) is { } upButton)
                    DeviceSet(_mouseButtonsDownByDevice, ev.Button.Which).Remove(upButton);
                break;
            case SDL.EventType.MouseMotion:
                state.MousePosition = new System.Numerics.Vector2(ev.Motion.X, ev.Motion.Y);
                state.MouseDelta += new System.Numerics.Vector2(ev.Motion.XRel, ev.Motion.YRel);
                break;
            case SDL.EventType.MouseWheel:
                state.WheelDelta += new System.Numerics.Vector2(ev.Wheel.X, ev.Wheel.Y);
                break;
        }
    }

    private static HashSet<T> DeviceSet<T>(Dictionary<uint, HashSet<T>> byDevice, uint deviceId) =>
        byDevice.TryGetValue(deviceId, out var set) ? set : byDevice[deviceId] = [];

    /// <summary>
    /// Reacts to <see cref="DeviceChange"/> (sourced from <see cref="PlatformSystem"/>, the
    /// single canonical emitter - see that type's own doc comment). Only disconnects need a
    /// reaction: a connect needs no seeding, since <see cref="DeviceSet{T}"/> already lazily
    /// creates a device's down-state entry on its first real key/button event. Runs after
    /// this tick's raw key/button events are already applied (in <see cref="Execute"/>), so
    /// a disconnect always wins over a same-tick key event for that device, regardless of
    /// the order SDL happened to deliver them in - the mid-press "stuck held" safety net
    /// this package guarantees.
    /// </summary>
    private void ApplyDeviceChanges()
    {
        foreach (var change in _deviceChanges.Read())
        {
            if (change.Change != DeviceChangeKind.Disconnected) continue;

            switch (change.DeviceKind)
            {
                case DeviceKind.Keyboard:
                    _keysDownByDevice.Remove(change.DeviceId);
                    break;
                case DeviceKind.Mouse:
                    _mouseButtonsDownByDevice.Remove(change.DeviceId);
                    break;
            }
            Bindings.UnassignDeviceById(change.DeviceId); // SDL device ids aren't stable across a reconnect, so a stale assignment serves no purpose
        }
    }

    private bool KeyIsDown(int profile, SDL.Scancode key)
    {
        var assigned = Bindings.AssignedDevicesFor(profile);
        if (assigned is null)
        {
            foreach (var set in _keysDownByDevice.Values)
                if (set.Contains(key)) return true;
            return false;
        }
        foreach (var deviceId in assigned)
            if (_keysDownByDevice.TryGetValue(deviceId, out var set) && set.Contains(key)) return true;
        return false;
    }

    private bool MouseButtonIsDown(int profile, MouseButton button)
    {
        var assigned = Bindings.AssignedDevicesFor(profile);
        if (assigned is null)
        {
            foreach (var set in _mouseButtonsDownByDevice.Values)
                if (set.Contains(button)) return true;
            return false;
        }
        foreach (var deviceId in assigned)
            if (_mouseButtonsDownByDevice.TryGetValue(deviceId, out var set) && set.Contains(button)) return true;
        return false;
    }
}
