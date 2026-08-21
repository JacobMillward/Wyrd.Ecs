using SDL3;

namespace Wyrd.Ecs.Platform;

/// <summary>
/// Registers a <see cref="PlatformSystem"/> on a <see cref="WorldBuilder"/>. Calls
/// <see cref="WorldBuilder.AddSystemCore"/> directly, not the generated
/// <c>AddSystem&lt;T&gt;(Func&lt;World, T&gt;)</c> sugar - needed because
/// <see cref="PlatformSystem"/>'s constructor takes more than just <c>World</c>, and so this
/// method can apply <see cref="Phase.PreUpdate"/> fluently via
/// <see cref="SystemRegistration.Phase"/> instead of a class attribute.
/// <see cref="PlatformSystem"/> itself declares no <see cref="PhaseAttribute"/> for the same
/// reason - it's no longer routed through the generator's <c>AddSystem&lt;T&gt;()</c> path at
/// all.
/// </summary>
public static class WorldBuilderPlatformExtensions
{
    extension(WorldBuilder builder)
    {
        /// <summary>
        /// Registers a <see cref="PlatformSystem"/> that opens a <paramref name="width"/>x<paramref name="height"/>
        /// window titled <paramref name="title"/>. No construction dependencies of its own -
        /// order-independent with respect to <c>AddRenderer</c>/<c>AddInput</c> in the same chain.
        /// </summary>
        public WorldBuilder AddWindow(string title, int width, int height, SDL.WindowFlags flags = default)
        {
            builder.AddSystemCore(
                typeof(PlatformSystem),
                access: null,
                construct: w => new PlatformSystem(w, title, width, height, flags),
                generatedBeforeTargets: [],
                generatedAfterTargets: [])
                .Phase(Phase.PreUpdate);
            return builder;
        }
    }
}
