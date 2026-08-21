using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Registers a <see cref="RendererSystem"/> on a <see cref="WorldBuilder"/>, bound to the
/// registered <see cref="PlatformSystem"/> - order-independent: <c>AddWindow</c> can be
/// called before or after this method in the same chain, since both declare their
/// construction relationship to <see cref="WorldBuilder.AddSystemCore"/> explicitly rather
/// than relying on call order. <see cref="WorldBuilder.Build"/> throws if <c>AddWindow</c>
/// was never called at all.
/// </summary>
public static class WorldBuilderRendererExtensions
{
    extension(WorldBuilder builder)
    {
        /// <summary>Registers a <see cref="RendererSystem"/> claiming the platform's window.</summary>
        public WorldBuilder AddRenderer()
        {
            builder.AddSystemCore(
                typeof(RendererSystem),
                access: null,
                construct: w => new RendererSystem(w, w.GetSystem<PlatformSystem>()),
                generatedBeforeTargets: [],
                generatedAfterTargets: [],
                constructionDependencies: [typeof(PlatformSystem)])
                .Phase(Phase.PostUpdate);
            return builder;
        }
    }
}
