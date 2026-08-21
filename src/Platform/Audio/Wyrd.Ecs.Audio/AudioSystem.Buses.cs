using SDL3;

namespace Wyrd.Ecs.Audio;

public sealed partial class AudioSystem
{
    private readonly Dictionary<(IntPtr Mixer, string Tag), float> _busGains = new();

    private static string TagFor(BusKind kind) => kind switch
    {
        BusKind.Master => "master",
        BusKind.Music => "music",
        BusKind.Sfx => "sfx",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The well-known <paramref name="kind"/> bus for <paramref name="output"/>
    /// (default: <see cref="DefaultOutput"/>). Two calls with the same arguments return equal
    /// values - <see cref="AudioBus"/> needs no internal caching for this to hold, its equality
    /// is purely structural.</summary>
    public AudioBus Bus(BusKind kind, AudioOutput? output = null) =>
        new(output ?? DefaultOutput, TagFor(kind));

    /// <summary>A custom bus beyond the three built-ins (e.g. "dialogue"), for
    /// <paramref name="output"/> (default: <see cref="DefaultOutput"/>).</summary>
    public AudioBus CustomBus(string name, AudioOutput? output = null) =>
        new(output ?? DefaultOutput, name);

    /// <summary>Sets every track tagged with <paramref name="bus"/>'s gain. Clamped to
    /// <c>[0f, 1f]</c>.</summary>
    public void SetBusVolume(AudioBus bus, float volume)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        var mixer = GetOutput(bus.Output).Mixer;
        var clamped = Math.Clamp(volume, 0f, 1f);
        if (!Mixer.SetTagGain(mixer, bus.Tag, clamped))
            throw new InvalidOperationException($"MIX_SetTagGain failed: {SDL.GetError()}");
        _busGains[(mixer, bus.Tag)] = clamped;
    }

    /// <summary>Reads back what <see cref="SetBusVolume"/> last set for <paramref name="bus"/>;
    /// <c>1f</c> (unity gain) if never set. <c>SDL3.Mixer</c> has no <c>GetTagGain</c> - this
    /// reads back what this system itself last set, not a native query.</summary>
    public float GetBusVolume(AudioBus bus)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        var mixer = GetOutput(bus.Output).Mixer;
        return _busGains.TryGetValue((mixer, bus.Tag), out var gain) ? gain : 1f;
    }
}
