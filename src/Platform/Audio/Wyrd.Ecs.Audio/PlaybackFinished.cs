namespace Wyrd.Ecs.Audio;

/// <summary>A <see cref="Playback"/> finished on its own (reached the end, non-looping) or was
/// stopped explicitly. Emitted exactly once per <see cref="Playback"/>. Subscribe via
/// <c>World.CreateEventReader&lt;PlaybackFinished&gt;()</c>, matching
/// <see cref="Wyrd.Ecs.Platform.DeviceChange"/>'s existing reader pattern.</summary>
public readonly record struct PlaybackFinished(Playback Playback) : IEvent;
