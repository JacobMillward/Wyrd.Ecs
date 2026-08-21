namespace Wyrd.Ecs.Audio;

/// <summary>One entry from <c>AudioSystem.GetAvailableOutputDevices</c> - an SDL audio
/// playback device id paired with its human-readable name, for a settings-menu device picker.</summary>
public readonly record struct AudioDeviceInfo(uint Id, string Name);
