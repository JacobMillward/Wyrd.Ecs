using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Registers a <see cref="RendererSystem"/> on a <see cref="WorldBuilder"/>, bound to the
/// already-registered <see cref="PlatformSystem"/>. Requires <c>AddPlatform</c> to have been
/// called earlier in the same builder chain, since <see cref="RendererSystem"/>'s constructor
/// resolves <see cref="PlatformSystem"/> via <see cref="World.GetSystem{T}"/> at
/// <c>Build()</c> time, which only finds systems constructed earlier in registration order.
/// </summary>
public static class WorldBuilderRendererExtensions
{
    extension(WorldBuilder builder)
    {
        /// <summary>Registers a <see cref="RendererSystem"/> claiming the platform's window.</summary>
        public WorldBuilder AddRenderer()
        {
            builder.AddSystem<RendererSystem>(w => new RendererSystem(w, w.GetSystem<PlatformSystem>()));
            return builder;
        }
    }
}
