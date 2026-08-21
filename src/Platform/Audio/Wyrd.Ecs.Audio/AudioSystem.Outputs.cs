using SDL3;

namespace Wyrd.Ecs.Audio;

public sealed partial class AudioSystem
{
    private sealed class Output
    {
        public required IntPtr Mixer;
    }

    private readonly List<Output?> _outputs = [];
    private readonly List<int> _outputGenerations = [];

    /// <summary>Which <see cref="AudioOutput"/> <c>Play</c>'s default <c>bus: null</c> and
    /// <c>Bus</c>/<c>CustomBus</c>'s default <c>output</c> resolve to. Set at construction to
    /// the device's first output; change with <see cref="SetDefaultOutput"/>.</summary>
    public AudioOutput DefaultOutput { get; private set; }

    /// <summary>Creates a new output bound to <paramref name="deviceId"/> (default: the OS's
    /// current default playback device). The very first call, made by the constructor, becomes
    /// <see cref="DefaultOutput"/>; later calls don't change it - use
    /// <see cref="SetDefaultOutput"/> for that.</summary>
    public AudioOutput AddOutput(uint deviceId = SDL.AudioDeviceDefaultPlayback)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        var mixer = Mixer.CreateMixerDevice(deviceId, IntPtr.Zero);
        if (mixer == IntPtr.Zero)
            throw new InvalidOperationException($"MIX_CreateMixerDevice failed: {SDL.GetError()}");

        var output = new Output { Mixer = mixer };
        _outputs.Add(output);
        _outputGenerations.Add(0);
        return new AudioOutput(_outputs.Count - 1, 0);
    }

    /// <summary>Changes which <see cref="AudioOutput"/> <see cref="DefaultOutput"/> resolves to,
    /// without destroying or recreating anything.</summary>
    public void SetDefaultOutput(AudioOutput output)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        GetOutput(output); // throws if stale, same guard as every other accessor below
        DefaultOutput = output;
    }

    /// <summary>Wraps <c>SDL_GetAudioPlaybackDevices</c>/<c>SDL_GetAudioDeviceName</c> for a
    /// settings-menu device picker.</summary>
    public IReadOnlyList<AudioDeviceInfo> GetAvailableOutputDevices()
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        var ids = SDL.GetAudioPlaybackDevices(out _) ?? [];
        var result = new List<AudioDeviceInfo>(ids.Length);
        foreach (var id in ids)
        {
            var name = SDL.GetAudioDeviceName(id) ?? $"Unknown device {id}";
            result.Add(new AudioDeviceInfo(id, name));
        }
        return result;
    }

    internal IntPtr GetOutputMixer(AudioOutput output) => GetOutput(output).Mixer;

    private Output GetOutput(AudioOutput output)
    {
        if (output.Index >= _outputs.Count || _outputs[output.Index] is not { } o || _outputGenerations[output.Index] != output.Generation)
            throw new InvalidOperationException($"AudioOutput {output} does not refer to a live output.");
        return o;
    }

    private void DestroyAllOutputs()
    {
        foreach (var output in _outputs)
        {
            if (output is null) continue;
            Mixer.DestroyMixer(output.Mixer);
        }
    }
}
