using SDL3;
using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Audio;

public sealed partial class AudioSystem
{
    private sealed class PlaybackSlot
    {
        public required IntPtr Track;
        public required IntPtr Mixer;
    }

    private readonly List<PlaybackSlot?> _playbacks = [];
    private readonly List<int> _playbackGenerations = [];

    /// <summary>Plays an already-loaded <paramref name="sound"/>. <paramref name="bus"/> null
    /// resolves to <c>Bus(BusKind.Sfx, DefaultOutput)</c> - the output this plays on is entirely
    /// determined by <paramref name="bus"/>, there's no separate output parameter. Prefer this
    /// overload over <see cref="Play(string, AudioBus?, float, bool, System.Numerics.Vector3?)"/>
    /// for anything played more than once - the string overload decodes fresh on every call,
    /// this one reuses the already-decoded <see cref="Sound"/>.</summary>
    public Playback Play(Handle<Sound> sound, AudioBus? bus = null, float volume = 1f, bool loop = false, System.Numerics.Vector3? position = null)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        var resolvedBus = bus ?? Bus(BusKind.Sfx);
        var mixer = GetOutput(resolvedBus.Output).Mixer;
        var audioObject = _soundArena.TryGet(sound) ?? throw new InvalidOperationException("Handle<Sound> is not yet loaded or failed to load; await WaitForLoadAsync first.");

        var track = Mixer.CreateTrack(mixer);
        if (!Mixer.SetTrackAudio(track, audioObject.AudioHandle))
            throw new InvalidOperationException($"MIX_SetTrackAudio failed: {SDL.GetError()}");

        return StartTrack(track, mixer, resolvedBus, volume, loop, position);
    }

    /// <summary>Streams <paramref name="path"/> directly - never cached, never dedup'd, opens a
    /// fresh <c>SDL_IOStream</c> and decodes on demand every call. For anything played more than
    /// once, prefer the <see cref="Play(Handle{Sound}, AudioBus?, float, bool, System.Numerics.Vector3?)"/>
    /// overload via <see cref="LoadSound"/> instead - this overload exists for genuinely one-off
    /// or long-running streamed content (a music track, a rarely-heard voice line) where caching
    /// it in the arena would just waste memory holding something played once, or never.</summary>
    public Playback Play(string path, AudioBus? bus = null, float volume = 1f, bool loop = false, System.Numerics.Vector3? position = null)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        var resolvedBus = bus ?? Bus(BusKind.Sfx);
        var mixer = GetOutput(resolvedBus.Output).Mixer;

        var io = SDL.IOFromFile(path, "rb");
        if (io == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_IOFromFile failed: {SDL.GetError()}");

        var track = Mixer.CreateTrack(mixer);
        if (!Mixer.SetTrackIOStream(track, io, closeio: true))
            throw new InvalidOperationException($"MIX_SetTrackIOStream failed: {SDL.GetError()}");

        return StartTrack(track, mixer, resolvedBus, volume, loop, position);
    }

    private Playback StartTrack(IntPtr track, IntPtr mixer, AudioBus bus, float volume, bool loop, System.Numerics.Vector3? position)
    {
        Mixer.TagTrack(track, bus.Tag);
        Mixer.SetTrackGain(track, Math.Clamp(volume, 0f, 1f));
        Mixer.SetTrackLoops(track, loop ? -1 : 0);
        if (!Mixer.PlayTrack(track, options: 0))
            throw new InvalidOperationException($"MIX_PlayTrack failed: {SDL.GetError()}");

        var slot = new PlaybackSlot { Track = track, Mixer = mixer };
        var freeIndex = _playbacks.FindIndex(s => s is null);
        if (freeIndex >= 0)
        {
            _playbacks[freeIndex] = slot;
            return new Playback(freeIndex, _playbackGenerations[freeIndex]);
        }
        _playbacks.Add(slot);
        _playbackGenerations.Add(0);
        return new Playback(_playbacks.Count - 1, 0);
    }

    /// <summary><c>true</c> if <paramref name="playback"/> hasn't finished or been stopped yet.</summary>
    public bool IsPlaying(Playback playback) => Mixer.TrackPlaying(GetPlaybackSlot(playback).Track);

    /// <summary>Stops <paramref name="playback"/>, fading out over <paramref name="fadeOut"/>
    /// (default: immediate).</summary>
    public void Stop(Playback playback, TimeSpan fadeOut = default)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        var slot = GetPlaybackSlot(playback);
        var fadeFrames = fadeOut == default ? 0L : (long)(fadeOut.TotalSeconds * GetOutputSampleRate(slot.Mixer));
        Mixer.StopTrack(slot.Track, fadeFrames);
    }

    /// <summary>Sets <paramref name="playback"/>'s gain, clamped to <c>[0f, 1f]</c>.</summary>
    public void SetVolume(Playback playback, float volume)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        Mixer.SetTrackGain(GetPlaybackSlot(playback).Track, Math.Clamp(volume, 0f, 1f));
    }

    private static unsafe long GetOutputSampleRate(IntPtr mixer)
    {
        var spec = new SDL.AudioSpec();
        if (!Mixer.GetMixerFormat(mixer, (IntPtr)(&spec)))
            throw new InvalidOperationException($"MIX_GetMixerFormat failed: {SDL.GetError()}");
        return spec.Freq;
    }

    private PlaybackSlot GetPlaybackSlot(Playback playback)
    {
        if (playback.Index >= _playbacks.Count || _playbacks[playback.Index] is not { } slot || _playbackGenerations[playback.Index] != playback.Generation)
            throw new InvalidOperationException($"Playback {playback} does not refer to a live track.");
        return slot;
    }
}
