namespace Wyrd.Ecs.Audio;

/// <summary>A reference to one output device's mixer. Only ever passed explicitly to
/// <c>AddOutput</c>, <c>SetDefaultOutput</c>, <c>Bus</c>/<c>CustomBus</c>, and
/// <c>SetListener</c> - every playback-level call infers its output from a <see cref="Playback"/>
/// or <c>AudioBus</c> instead.</summary>
public readonly record struct AudioOutput(int Index, int Generation);
