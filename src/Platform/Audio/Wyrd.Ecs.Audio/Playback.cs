namespace Wyrd.Ecs.Audio;

/// <summary>A cheap-to-copy reference to a live playback instance, not an asset. Same shape as
/// <see cref="Wyrd.Ecs.Assets.Handle{T}"/> - a stale <see cref="Playback"/> (already stopped, or
/// from before a slot got reused) throws rather than silently resolving to the wrong track.
/// Methods that act on one (<c>Stop</c>, <c>SetVolume</c>, <c>IsPlaying</c>, <c>Follow</c>) live
/// on <c>AudioSystem</c>, not here.</summary>
public readonly record struct Playback(int Index, int Generation);
