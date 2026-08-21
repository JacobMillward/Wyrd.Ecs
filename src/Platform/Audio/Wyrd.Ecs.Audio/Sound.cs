namespace Wyrd.Ecs.Audio;

/// <summary>A loaded sound effect's SDL_mixer identity (a <c>MIX_Audio*</c>). Never held
/// directly by a caller, always through a <see cref="Wyrd.Ecs.Assets.Handle{T}"/>, so
/// <c>AudioSystem.Unload</c> can't invalidate a still-referenced handle. Fields and
/// constructor stay <c>internal</c>; the class itself is public only because
/// <see cref="Wyrd.Ecs.Assets.Handle{T}"/>'s type argument must be at least as accessible as
/// wherever a <c>Handle&lt;Sound&gt;</c> is stored.</summary>
public sealed class Sound
{
    internal readonly IntPtr AudioHandle;

    internal Sound(IntPtr audioHandle)
    {
        AudioHandle = audioHandle;
    }
}
