using Wyrd.Ecs.Assets;
using Wyrd.Ecs.Renderer;

namespace Wyrd.Ecs.Examples.Asteroids.Templates;

/// <summary>Shared asteroid look. Transform, Velocity, Spin, Asteroid, and the size-based
/// tint are added per-spawn by <see cref="AsteroidSpawner.Spawn"/>.</summary>
public sealed class AsteroidTemplate : EntityTemplate
{
    public AsteroidTemplate(Handle<Texture> asteroidTexture)
    {
        AddComponent(new Sprite(SourceRect: null, Tint: Color.White));
        AddComponent(new Material(ShaderKind.UnlitSprite, asteroidTexture, BlendMode.Transparent));
    }
}
