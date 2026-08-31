using Wyrd.Ecs.Assets;
using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Renderer;

namespace Wyrd.Ecs.Examples.Asteroids.Templates;

/// <summary>A player bullet: expires via <see cref="Lifetime"/>. Transform and Velocity are
/// added per-shot by <see cref="Systems.WeaponSystem"/>, since they depend on the ship's
/// heading and speed at the moment of firing.</summary>
public sealed class BulletTemplate : EntityTemplate
{
    public BulletTemplate(Handle<Texture> bulletTexture)
    {
        AddTag<Bullet>();
        AddComponent(new Lifetime { SecondsRemaining = 1.1f });
        AddComponent(new Sprite(SourceRect: null, Tint: Color.White));
        AddComponent(new Material(ShaderKind.UnlitSprite, bulletTexture, BlendMode.Transparent));
    }
}
