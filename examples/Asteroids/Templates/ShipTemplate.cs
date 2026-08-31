using System.Numerics;
using Wyrd.Ecs.Assets;
using Wyrd.Ecs.Examples.Asteroids.Components;
using Wyrd.Ecs.Renderer;

namespace Wyrd.Ecs.Examples.Asteroids.Templates;

/// <summary>The player ship: a Ship/Velocity/Transform sprite with an engine-flame child,
/// invisible until <see cref="Systems.ShipControlSystem"/> toggles its tint alpha on thrust.</summary>
public sealed class ShipTemplate : EntityTemplate
{
    public ShipTemplate(Handle<Texture> shipTexture, Handle<Texture> flameTexture)
    {
        AddTransform(Vector3.Zero);
        AddComponent(new Velocity());
        AddComponent(new Ship());
        AddComponent(new Sprite(SourceRect: null, Tint: Color.White));
        AddComponent(new Material(ShaderKind.UnlitSprite, shipTexture, BlendMode.Transparent));
        AddChild(new EngineFlameTemplate(flameTexture));
    }
}

file sealed class EngineFlameTemplate : EntityTemplate
{
    public EngineFlameTemplate(Handle<Texture> flameTexture)
    {
        AddTransform(new Vector3(-28f, 0f, 0f));
        AddComponent(new Sprite(SourceRect: null, Tint: Color.White with { A = 0f }));
        AddComponent(new Material(ShaderKind.UnlitSprite, flameTexture, BlendMode.Transparent));
        AddTag<EngineFlame>();
    }
}
