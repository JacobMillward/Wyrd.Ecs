using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// <see cref="Sprite"/> plus <see cref="Material"/> in one call, the pair every 2D drawable
/// entity needs. <see cref="Transform"/> stays a separate, chained call, positioning is
/// orthogonal to what makes an entity a sprite.
/// </summary>
public readonly record struct SpriteBundle(Handle<Texture> Texture, Rect? SourceRect = null, Color? Tint = null, ShaderKind? ShaderKind = null) : IComponentBundle
{
    /// <inheritdoc/>
    public void ApplyTo<TSink>(TSink sink) where TSink : IComponentSink, allows ref struct =>
        new BundleBuilder<TSink>(sink)
            .Add(new Sprite(SourceRect, Tint ?? Color.White))
            .Add(new Material(ShaderKind ?? Renderer.ShaderKind.UnlitSprite, Texture));
}
