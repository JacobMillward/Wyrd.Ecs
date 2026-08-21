using SDL3;
using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Audio;

public sealed partial class AudioSystem
{
    private readonly AssetArena<string, Sound> _soundArena = new();

    /// <summary>
    /// Allocates a <see cref="Handle{T}"/> immediately and starts a background decode via
    /// <c>MIX_LoadAudio</c>, which is thread-safe, so unlike
    /// <c>Wyrd.Ecs.Renderer.RendererSystem.LoadTexture</c> this needs no
    /// device-thread/copy-pass handoff. Calling this again with a path already reserved returns
    /// the existing handle without re-decoding - <c>AssetArena.Reserve</c>'s <c>isNew</c>
    /// out-param, same fix as the one applied to <c>Wyrd.Ecs.Renderer</c>'s own texture loading.
    /// </summary>
    public Handle<Sound> LoadSound(string path)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        var handle = _soundArena.Reserve(path, out var isNew);
        if (!isNew) return handle;

        var mixer = GetOutput(DefaultOutput).Mixer;
        Task.Run(() =>
        {
            var audio = Mixer.LoadAudio(mixer, path, predecode: true);
            if (audio == IntPtr.Zero)
            {
                _soundArena.MarkFailed(handle, new InvalidOperationException($"MIX_LoadAudio failed: {SDL.GetError()}"));
                return;
            }
            _soundArena.MarkLoaded(handle, new Sound(audio));
        });

        return handle;
    }

    /// <summary>Task that completes (or faults with the captured decode/IO exception) once
    /// <paramref name="handle"/> resolves.</summary>
    public Task WaitForLoadAsync(Handle<Sound> handle)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        return _soundArena.WaitForLoadAsync(handle);
    }

    /// <summary>Decrements the handle's use-count; once it reaches zero, destroys the
    /// underlying <c>MIX_Audio</c> immediately - unlike GPU textures, there's no
    /// frames-in-flight timing concern here, SDL_mixer owns its own thread-safety for this.</summary>
    public void Unload(Handle<Sound> handle)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        if (!_soundArena.Unload(handle, out var sound) || sound is null) return;
        Mixer.DestroyAudio(sound.AudioHandle);
    }
}
