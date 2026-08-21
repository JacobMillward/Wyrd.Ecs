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
    /// overload over the <c>Play(string, ...)</c> overload for anything played more than once -
    /// the string overload decodes fresh on every call, this one reuses the already-decoded
    /// <see cref="Sound"/>.</summary>
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

    private PlaybackSlot GetPlaybackSlot(Playback playback)
    {
        if (playback.Index >= _playbacks.Count || _playbacks[playback.Index] is not { } slot || _playbackGenerations[playback.Index] != playback.Generation)
            throw new InvalidOperationException($"Playback {playback} does not refer to a live track.");
        return slot;
    }
}
