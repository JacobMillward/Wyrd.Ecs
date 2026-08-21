using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// <see cref="MeshRenderer"/> plus <see cref="Material"/> in one call, for a single drawable
/// mesh entity, not a multi-part model (see <c>ToEntityTemplate()</c> for that).
/// <see cref="Transform"/> stays a separate, chained call.
/// </summary>
public readonly record struct MeshBundle(Handle<Mesh> Mesh, Handle<Texture>? Texture = null, Color? Tint = null, ShaderKind? ShaderKind = null) : IComponentBundle
{
    /// <inheritdoc/>
    public void ApplyTo<TSink>(TSink sink) where TSink : IComponentSink, allows ref struct =>
        new BundleBuilder<TSink>(sink)
            .Add(new MeshRenderer(Mesh, Tint ?? Color.White))
            .Add(new Material(ShaderKind ?? Renderer.ShaderKind.UnlitMesh, Texture));
}
