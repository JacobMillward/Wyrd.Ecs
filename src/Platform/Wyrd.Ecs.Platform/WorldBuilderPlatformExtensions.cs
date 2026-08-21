using SDL3;

namespace Wyrd.Ecs.Platform;

/// <summary>
/// Registers a <see cref="PlatformSystem"/> on a <see cref="WorldBuilder"/>. A thin wrapper
/// over <c>AddSystem&lt;PlatformSystem&gt;(Func&lt;World, PlatformSystem&gt;)</c>, needed
/// because <see cref="PlatformSystem"/>'s constructor takes more than just <c>World</c>, so
/// the generator's parameterless/ctor(World)-only <c>AddSystem&lt;T&gt;()</c> overload can't
/// construct it.
/// </summary>
public static class WorldBuilderPlatformExtensions
{
    extension(WorldBuilder builder)
    {
        /// <summary>Registers a <see cref="PlatformSystem"/> that opens a <paramref name="width"/>x<paramref name="height"/> window titled <paramref name="title"/>.</summary>
        public WorldBuilder AddWindow(string title, int width, int height, SDL.WindowFlags flags = default)
        {
            builder.AddSystem<PlatformSystem>(w => new PlatformSystem(w, title, width, height, flags));
            return builder;
        }
    }
}
