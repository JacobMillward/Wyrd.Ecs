---
title: Audio
description: Sound effects and music over SDL_mixer - loading, buses, outputs, and spatial positioning.
---

`Wyrd.Ecs.Audio` plays sounds through SDL_mixer. `AddAudio()` needs `AddWindow` called somewhere in the same chain - not for its own sake, but because audio-device hot-plug depends on `PlatformSystem` being the one process pumping SDL's event queue. Construction order doesn't matter.

## Playing a sound

```csharp
public sealed partial class GunfireSystem : QuerySystem
{
    [Resource] public AudioPlayer Audio { get; private set; }

    protected override IQuery DefineQuery(Query query) => query.With<ShotFired>();

    public void Update(Time time, ref ShotFired shot)
    {
        Audio.Play("audio/shot.wav", volume: 0.8f);
    }
}
```

`AudioPlayer` is a resource registered automatically by `AddAudio()`, so systems reach it with `[Resource]` injection like any other. `Play(string path)` streams straight off disk - fine for fire-and-forget effects, but nothing is cached or dedup'd, every call re-opens the file.

For sounds played often, load once and reuse:

```csharp
Handle<Sound> shot = Audio.LoadSound("audio/shot.wav");
await Audio.WaitForLoadAsync(shot);

Audio.Play(shot); // decodes once in the background, then plays from memory
```

Loading is dedup'd by path: a second `LoadSound` for a file already reserved returns the existing handle without decoding again. `Unload(handle)` decrements a use-count, the underlying decode is destroyed when the last handle goes.

Every `Play` returns a `Playback`, a cheap-to-copy reference to that live instance:

```csharp
Playback music = Audio.Play(shot, loop: true);

Audio.SetVolume(music, 0.5f); // clamped to [0, 1]
if (Audio.IsPlaying(music)) { /* ... */ }
Audio.Stop(music, fadeOut: TimeSpan.FromSeconds(2));
```

A stale `Playback` (already stopped) throws rather than silently doing nothing.

:::tip
When a playback ends on its own - reached the end without looping, or stopped explicitly - an `IEvent` lands in the usual event pipeline exactly once per `Playback`. Read it with `world.CreateEventReader<PlaybackFinished>()`.
:::

## Buses

Volume control routes through buses. Three are built in: `Master` (everything), `Music`, and `Sfx` (`Play`'s default). Custom ones are just names:

```csharp
Audio.SetBusVolume(Audio.Bus(BusKind.Master), 0.9f);
var dialogue = Audio.CustomBus("dialogue");
Audio.Play(shot, bus: dialogue);
```

Buses are scoped per output device - `Bus(BusKind.Sfx)` on two different outputs are two genuinely different buses.

## Outputs

By default everything plays through the OS's default output device. For more - a second headset, split music to speakers and voice chat to headphones - enumerate what's connected and add outputs explicitly:

```csharp
foreach (var device in Audio.GetAvailableOutputDevices())
    Console.WriteLine(device.Name);

AudioOutput headphones = Audio.AddOutput(device.Id);
Audio.SetDefaultOutput(headphones);
```

Devices connecting and disconnecting mid-session are picked up by the same event pump that drives windowing and surface as `DeviceChange` events (`DeviceKind.AudioOutput`), so `GetAvailableOutputDevices` reflects whatever is connected when you call it.

## Spatial audio

Give `Play` a world position and it plays there relative to the listener:

```csharp
Audio.Play(shot, position: new Vector3(10f, 0f, 0f));
```

The listener is an entity whose `Transform` positions the output's ear:

```csharp
Audio.SetListener(Audio.DefaultOutput, cameraEntity);
```

For a sound attached to a moving entity, `Follow` tracks its interpolated transform every tick instead of a fixed point:

```csharp
Playback engineHum = Audio.Play(engineSound, loop: true);
Audio.Follow(engineHum, enemyEntity);
```

`Follow` ties the sound to that entity's life: if the entity is destroyed mid-playback, the playback stops with it. A sound that should outlive its trigger takes a fixed `position` instead. If no listener was ever set, positions fall back to the origin; if the listener entity dies, sources freeze relative to its last known transform rather than snapping anywhere.

## Next

[Assets](/build/assets/) covers the dedup-and-use-count loading pattern `LoadSound` is built on, reusable for loaders of your own.
