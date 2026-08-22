using System.Runtime.InteropServices;
using SDL3;
using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Audio;

public sealed partial class AudioSystem
{
    private sealed class PlaybackSlot
    {
        public required IntPtr Track;
        public required IntPtr Mixer;
        public required GCHandle CallbackHandle;
    }

    private readonly List<PlaybackSlot?> _playbacks = [];
    private readonly List<int> _playbackGenerations = [];
    private readonly Dictionary<Playback, System.Numerics.Vector3> _fixedPositions = new();

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

    private readonly System.Collections.Concurrent.ConcurrentQueue<Playback> _finishedPending = new();

    private Playback StartTrack(IntPtr track, IntPtr mixer, AudioBus bus, float volume, bool loop, System.Numerics.Vector3? position)
    {
        Mixer.TagTrack(track, bus.Tag);
        Mixer.SetTrackGain(track, Math.Clamp(volume, 0f, 1f));
        Mixer.SetTrackLoops(track, loop ? -1 : 0);

        var freeIndex = _playbacks.FindIndex(s => s is null);
        int index, generation;
        if (freeIndex >= 0)
        {
            index = freeIndex;
            generation = _playbackGenerations[freeIndex];
        }
        else
        {
            _playbacks.Add(null);
            _playbackGenerations.Add(0);
            index = _playbacks.Count - 1;
            generation = 0;
        }
        var playback = new Playback(index, generation);

        if (position is { } fixedPosition)
            _fixedPositions[playback] = fixedPosition;

        // Fires on whatever thread the mixer's internal audio callback runs on - it must not
        // touch _playbacks or anything else non-thread-safe directly, so it only ever hands the
        // finished Playback to the thread-safe queue. FinishTrack (called from Execute, main
        // thread only) does the actual cleanup, including freeing this callback's GCHandle - see
        // the GCHandle.Alloc call below for why that has to stay single-threaded.
        Mixer.TrackStoppedCallback callback = (IntPtr userdata, IntPtr stoppedTrack) =>
            _finishedPending.Enqueue(playback);

        // Keeps the callback delegate alive independent of AudioSystem's own reachability:
        // SetTrackStoppedCallback hands SDL_mixer a native function pointer, invoked
        // asynchronously at some point after this method returns. A field or list reference
        // isn't enough - the whole AudioSystem (and everything it owns) can itself become
        // unreachable and get collected while a track is still playing, taking the delegate
        // down with it. Freed exactly once, from the main thread only, in FinishTrack or
        // FreeRemainingPlaybackCallbackHandles - never from the callback above, since
        // GCHandle.Free isn't safe to race against those.
        var callbackHandle = GCHandle.Alloc(callback);
        _playbacks[index] = new PlaybackSlot { Track = track, Mixer = mixer, CallbackHandle = callbackHandle };
        Mixer.SetTrackStoppedCallback(track, callback, IntPtr.Zero);

        if (!Mixer.PlayTrack(track, options: 0))
            throw new InvalidOperationException($"MIX_PlayTrack failed: {SDL.GetError()}");
        return playback;
    }

    /// <summary>Runs the single-threaded half of a track's finish - called from
    /// <see cref="Execute"/> once its <c>MIX_TrackStoppedCallback</c> has already fired (natural
    /// completion or an explicit <see cref="Stop"/>), confirmed by the <see cref="Playback"/>'s
    /// presence in <see cref="_finishedPending"/>. Frees the slot's <see cref="GCHandle"/>,
    /// destroys the now-idle native track, and recycles the slot for reuse by a future
    /// <see cref="StartTrack"/> call.</summary>
    private void FinishTrack(Playback playback)
    {
        if (playback.Index >= _playbacks.Count || _playbacks[playback.Index] is not { } slot || _playbackGenerations[playback.Index] != playback.Generation)
            return; // shouldn't happen - each Playback finishes exactly once - but a no-op is cheap insurance against a bookkeeping mismatch
        slot.CallbackHandle.Free();
        Mixer.DestroyTrack(slot.Track);
        _playbacks[playback.Index] = null;
        _playbackGenerations[playback.Index]++;
        _fixedPositions.Remove(playback);
        _following.Remove(playback);
    }

    /// <summary><c>MIX_DestroyMixer</c> (called by <see cref="DestroyAllOutputs"/> right after
    /// this runs, from <see cref="OnDestroy"/>) destroys every track still alive on it without
    /// firing <c>MIX_TrackStoppedCallback</c> - confirmed via SDL_mixer's own docs for
    /// <c>MIX_DestroyTrack</c>. Without this, any track still playing at shutdown would leak its
    /// <see cref="GCHandle"/> for the rest of the process's lifetime.</summary>
    private void FreeRemainingPlaybackCallbackHandles()
    {
        foreach (var slot in _playbacks)
            slot?.CallbackHandle.Free();
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
