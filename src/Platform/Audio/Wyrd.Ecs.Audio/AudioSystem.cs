using SDL3;

namespace Wyrd.Ecs.Audio;

/// <summary>
/// Owns SDL's Audio subsystem lifecycle and every <see cref="AudioOutput"/>'s mixer.
/// <c>SDL_Init(Audio)</c>/<c>MIX_Init</c> run in the constructor, cleanup in
/// <see cref="OnDestroy"/> - matches <see cref="Wyrd.Ecs.Platform.PlatformSystem"/>'s own
/// "own subsystem init/quit in constructor/OnDestroy" convention. Independent of
/// <see cref="Wyrd.Ecs.Platform.PlatformSystem"/> for device lifecycle (<c>SDL_Init(Audio)</c>
/// has nothing to do with windowing) but depends on it being registered in the same
/// <see cref="World"/> for hot-plug - the construction dependency in
/// <see cref="WorldBuilderAudioExtensions.AddAudio"/> enforces this.
/// </summary>
[Phase(Phase.PostUpdate)]
public sealed partial class AudioSystem : EcsSystem
{
    private bool _destroyed;

    /// <summary>Calls <c>SDL_Init(Audio)</c> and <c>MIX_Init</c>, then creates the first
    /// <see cref="AudioOutput"/> (becoming <see cref="DefaultOutput"/>) bound to the OS's
    /// current default playback device. Throws <see cref="InvalidOperationException"/> if any
    /// step fails, wrapping <c>SDL_GetError()</c>.</summary>
    public AudioSystem(World world)
    {
        if (!SDL.Init(SDL.InitFlags.Audio))
            throw new InvalidOperationException($"SDL_Init(Audio) failed: {SDL.GetError()}");
        if (!Mixer.Init())
            throw new InvalidOperationException($"MIX_Init failed: {SDL.GetError()}");

        DefaultOutput = AddOutput(SDL.AudioDeviceDefaultPlayback);
        world.AddResource(new AudioPlayer(this));
    }

    /// <inheritdoc/>
    protected override void Execute(World world, Time time)
    {
        EnsureDeviceChangeReader(world);
        while (_finishedPending.TryDequeue(out var playback))
            world.Emit(new PlaybackFinished(playback));
        UpdateSpatialPlaybacks(world);
        ApplyDeviceChanges();
    }

    /// <inheritdoc/>
    protected override void OnDestroy()
    {
        _destroyed = true;
        var teardownException = new ObjectDisposedException(nameof(AudioSystem), "The audio system was destroyed before this asset finished loading.");
        _soundArena.FaultAllPending(teardownException);
        DestroyAllOutputs();
        Mixer.Quit();
        SDL.QuitSubSystem(SDL.InitFlags.Audio);
    }
}
