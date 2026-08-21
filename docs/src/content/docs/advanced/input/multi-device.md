---
title: Multi-Device
description: Assigning specific keyboards and mice to profiles, for more than one player on one machine.
---

A `BindingTable<TAction>` supports more than one player through `profile`, a `ProfileId` everywhere its methods default to `default` (profile 0). Two players sharing a keyboard by key range (WASD vs. arrow keys) need nothing more than two `BindAxis2D` calls at different profiles, see [Input](/engine/input/). Two players each on their own physical keyboard need one more step: telling each profile which device is actually theirs.

## Why profiles need device assignment

```csharp
var bindings = new BindingTable<PlayerAction>()
    .BindAxis2D(profile: new ProfileId(0), PlayerAction.Move, SDL.Scancode.W, SDL.Scancode.S, SDL.Scancode.A, SDL.Scancode.D)
    .BindAxis2D(profile: new ProfileId(1), PlayerAction.Move, SDL.Scancode.W, SDL.Scancode.S, SDL.Scancode.A, SDL.Scancode.D);
```

Both profiles bind the same keys. Left unassigned, a profile merges every connected device of the relevant kind, so either keyboard pressing `W` satisfies both profiles at once, not what two separate keyboards are supposed to mean.

## Assigning devices

```csharp
bindings.AssignDevice(profile: new ProfileId(0), deviceId: firstKeyboardId)
        .AssignDevice(profile: new ProfileId(1), deviceId: secondKeyboardId);
```

Once a profile has an assigned device, it only reads from that device, or devices, `AssignDevice` is additive, call it again to also track a mouse alongside a keyboard for the same profile. `UnassignDevice(profile)` clears it back to merging every connected device.

:::note
Where does a `deviceId` come from? Usually a `DeviceChange.Connected` event's `DeviceId` (see [Platform](/engine/platform/#devices-connecting-and-disconnecting)), captured during a "press a key to join" flow rather than hardcoded.
:::

## Reading per-profile state

```csharp
Input.TryGet(PlayerAction.Move, out var player1Move, profile: new ProfileId(0));
Input.TryGet(PlayerAction.Move, out var player2Move, profile: new ProfileId(1));
```

## Hot-plug during play

A disconnect clears that device's held keys immediately, before the next tick resolves any action, a device dropping mid-press can't leave an action stuck held. `IntentSystem<TAction>` handles this for every profile automatically, reacting to the disconnect itself (pausing, showing a reconnect prompt) still means reading `DeviceChange` yourself, see [Platform](/engine/platform/#devices-connecting-and-disconnecting).

## Next

[Remapping](/advanced/input/remapping/) for saving player-customized bindings, device assignments aren't part of what gets saved.
