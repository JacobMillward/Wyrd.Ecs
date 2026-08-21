namespace Wyrd.Ecs.Audio;

/// <summary>Which of the three built-in buses a <see cref="AudioBus"/> represents. Custom buses
/// (via <c>AudioSystem.CustomBus</c>) aren't part of this enum - they're just a name.</summary>
public enum BusKind
{
    /// <summary>The overall output bus - every other bus routes through this one.</summary>
    Master,

    /// <summary>The music bus.</summary>
    Music,

    /// <summary>The sound-effects bus - <c>AudioSystem.Play</c>'s default bus.</summary>
    Sfx,
}

/// <summary>A mixing bus, scoped to one <see cref="AudioOutput"/> by construction - SDL_mixer's
/// own tag mechanism is scoped per-mixer, so there's no such thing as a bus independent of an
/// output. Two <see cref="AudioBus"/> values are equal only if both the output and the tag
/// match: <c>Bus(BusKind.Sfx)</c> on two different outputs are genuinely different values, not
/// just conceptually different ones.</summary>
public readonly record struct AudioBus(AudioOutput Output, string Tag);
