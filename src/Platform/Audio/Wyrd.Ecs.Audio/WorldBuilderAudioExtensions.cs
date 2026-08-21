using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Audio;

/// <summary>
/// Registers an <see cref="AudioSystem"/> on a <see cref="WorldBuilder"/>. Requires
/// <c>AddWindow</c> in the same chain - not because <see cref="AudioSystem"/> needs a
/// <see cref="PlatformSystem"/> reference for device lifecycle (it doesn't), but because
/// audio-device hot-plug depends on <see cref="PlatformSystem"/> being the one process pumping
/// SDL's event queue; see <see cref="AudioSystem"/>'s own doc comment.
/// </summary>
public static class WorldBuilderAudioExtensions
{
    extension(WorldBuilder builder)
    {
        /// <summary>Registers an <see cref="AudioSystem"/>.</summary>
        public WorldBuilder AddAudio()
        {
            builder.AddSystemCore(
                typeof(AudioSystem),
                access: null,
                construct: _ => new AudioSystem(),
                generatedBeforeTargets: [],
                generatedAfterTargets: [],
                constructionDependencies: [typeof(PlatformSystem)])
                .Phase(Phase.PostUpdate);
            return builder;
        }
    }
}
