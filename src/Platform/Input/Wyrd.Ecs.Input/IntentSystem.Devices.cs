using SDL3;

namespace Wyrd.Ecs.Input;

public sealed partial class IntentSystem<TAction>
{
    private readonly Dictionary<uint, HashSet<SDL.Scancode>> _keysDownByDevice = [];
    private readonly Dictionary<uint, HashSet<MouseButton>> _mouseButtonsDownByDevice = [];

    private void HandleEvent(SDL.Event ev, ref IntentState<TAction> state)
    {
        switch ((SDL.EventType)ev.Type)
        {
            case SDL.EventType.KeyboardAdded:
                _keysDownByDevice.TryAdd(ev.KDevice.Which, []);
                state.ConnectedThisTickList.Add(ev.KDevice.Which);
                break;
            case SDL.EventType.KeyboardRemoved:
                HandleDeviceRemoved(_keysDownByDevice, ev.KDevice.Which, ref state);
                break;
            case SDL.EventType.MouseAdded:
                _mouseButtonsDownByDevice.TryAdd(ev.MDevice.Which, []);
                state.ConnectedThisTickList.Add(ev.MDevice.Which);
                break;
            case SDL.EventType.MouseRemoved:
                HandleDeviceRemoved(_mouseButtonsDownByDevice, ev.MDevice.Which, ref state);
                break;
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

    private void HandleDeviceRemoved<T>(Dictionary<uint, HashSet<T>> byDevice, uint deviceId, ref IntentState<TAction> state)
    {
        byDevice.Remove(deviceId); // clears this device's down-state immediately - a mid-press disconnect can never leave an action stuck held
        Bindings.UnassignDeviceById(deviceId); // SDL device ids aren't stable across a reconnect, so a stale assignment serves no purpose
        state.DisconnectedThisTickList.Add(deviceId);
    }

    private bool KeyIsDown(int seat, SDL.Scancode key)
    {
        var assigned = Bindings.AssignedDevicesFor(seat);
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

    private bool MouseButtonIsDown(int seat, MouseButton button)
    {
        var assigned = Bindings.AssignedDevicesFor(seat);
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
