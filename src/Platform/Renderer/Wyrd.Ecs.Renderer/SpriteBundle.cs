using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// <see cref="Sprite"/> plus <see cref="Material"/> in one call, the pair every 2D drawable
/// entity needs. <see cref="Transform"/> stays a separate, chained call, positioning is
/// orthogonal to what makes an entity a sprite. Defaults <see cref="BlendMode"/> to
/// <see cref="Renderer.BlendMode.Transparent"/>, unlike <see cref="Material"/>'s own
/// conservative <see cref="Renderer.BlendMode.Opaque"/> default (correct for
/// <see cref="MeshBundle"/>'s solid-3D-geometry common case): 2D sprite art almost always
/// has meaningful alpha at its edges, matching how Bevy/Godot/Unity's 2D sprite pipelines
/// blend by default too.
/// </summary>
public readonly record struct SpriteBundle(Handle<Texture> Texture, Rect? SourceRect = null, Color? Tint = null, ShaderKind? ShaderKind = null, BlendMode? BlendMode = null) : IComponentBundle
{
    /// <inheritdoc/>
    public void ApplyTo<TSink>(TSink sink) where TSink : IComponentSink, allows ref struct =>
        new BundleBuilder<TSink>(sink)
            .Add(new Sprite(SourceRect, Tint ?? Color.White))
            .Add(new Material(ShaderKind ?? Renderer.ShaderKind.UnlitSprite, Texture, BlendMode ?? Renderer.BlendMode.Transparent));
}
