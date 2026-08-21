using System.Numerics;
using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Audio;

/// <summary>
/// `[Resource]`-injectable audio, wrapping <see cref="AudioSystem"/>'s full surface. Registered
/// automatically by <c>AddAudio()</c>, so a system wanting to load or play something declares
/// <c>[Resource] public AudioPlayer Audio { get; private set; }</c> (reactive, per-tick) or
/// <c>in AudioPlayer audio</c> on its constructor (resolved once) instead of calling
/// <c>world.GetSystem&lt;AudioSystem&gt;()</c> directly. <see cref="AudioSystem"/>'s own methods
/// stay public too - this is additive convenience, matching <c>Wyrd.Ecs.Renderer.RenderAssets</c>'s
/// relationship to <c>RendererSystem</c>.
/// </summary>
public readonly record struct AudioPlayer(AudioSystem Audio) : IResource
{
    /// <summary>Same as <see cref="AudioSystem.LoadSound"/>.</summary>
    public Handle<Sound> LoadSound(string path) => Audio.LoadSound(path);

    /// <summary>Same as <see cref="AudioSystem.WaitForLoadAsync"/>.</summary>
    public Task WaitForLoadAsync(Handle<Sound> handle) => Audio.WaitForLoadAsync(handle);

    /// <summary>Same as <see cref="AudioSystem.Unload"/>.</summary>
    public void Unload(Handle<Sound> handle) => Audio.Unload(handle);

    /// <summary>Same as <see cref="AudioSystem.GetAvailableOutputDevices"/>.</summary>
    public IReadOnlyList<AudioDeviceInfo> GetAvailableOutputDevices() => Audio.GetAvailableOutputDevices();

    /// <summary>Same as <see cref="AudioSystem.AddOutput"/>.</summary>
    public AudioOutput AddOutput(uint deviceId = SDL3.SDL.AudioDeviceDefaultPlayback) => Audio.AddOutput(deviceId);

    /// <summary>Same as <see cref="AudioSystem.SetDefaultOutput"/>.</summary>
    public void SetDefaultOutput(AudioOutput output) => Audio.SetDefaultOutput(output);

    /// <summary>Same as <see cref="AudioSystem.DefaultOutput"/>.</summary>
    public AudioOutput DefaultOutput => Audio.DefaultOutput;

    /// <summary>Same as <see cref="AudioSystem.Bus"/>.</summary>
    public AudioBus Bus(BusKind kind, AudioOutput? output = null) => Audio.Bus(kind, output);

    /// <summary>Same as <see cref="AudioSystem.CustomBus"/>.</summary>
    public AudioBus CustomBus(string name, AudioOutput? output = null) => Audio.CustomBus(name, output);

    /// <summary>Same as <see cref="AudioSystem.SetBusVolume"/>.</summary>
    public void SetBusVolume(AudioBus bus, float volume) => Audio.SetBusVolume(bus, volume);

    /// <summary>Same as <see cref="AudioSystem.GetBusVolume"/>.</summary>
    public float GetBusVolume(AudioBus bus) => Audio.GetBusVolume(bus);

    /// <summary>Same as <see cref="AudioSystem.Play(Handle{Sound}, AudioBus?, float, bool, Vector3?)"/>.</summary>
    public Playback Play(Handle<Sound> sound, AudioBus? bus = null, float volume = 1f, bool loop = false, Vector3? position = null) =>
        Audio.Play(sound, bus, volume, loop, position);

    /// <summary>Same as <see cref="AudioSystem.Play(string, AudioBus?, float, bool, Vector3?)"/>.</summary>
    public Playback Play(string path, AudioBus? bus = null, float volume = 1f, bool loop = false, Vector3? position = null) =>
        Audio.Play(path, bus, volume, loop, position);

    /// <summary>Same as <see cref="AudioSystem.Stop"/>.</summary>
    public void Stop(Playback playback, TimeSpan fadeOut = default) => Audio.Stop(playback, fadeOut);

    /// <summary>Same as <see cref="AudioSystem.SetVolume"/>.</summary>
    public void SetVolume(Playback playback, float volume) => Audio.SetVolume(playback, volume);

    /// <summary>Same as <see cref="AudioSystem.IsPlaying"/>.</summary>
    public bool IsPlaying(Playback playback) => Audio.IsPlaying(playback);

    /// <summary>Same as <see cref="AudioSystem.Follow"/>.</summary>
    public void Follow(Playback playback, Entity entity) => Audio.Follow(playback, entity);

    /// <summary>Same as <see cref="AudioSystem.SetListener"/>.</summary>
    public void SetListener(AudioOutput output, Entity entity) => Audio.SetListener(output, entity);
}
