namespace Wyrd.Ecs.Renderer;

/// <summary>
/// An <see cref="OrthographicCamera"/> entity in one call. <see cref="Size"/> is half the
/// vertical world-space extent, not an angle. `Order`/`ClearOnBegin` default to the common
/// single-camera case (0, true); `Size`/near/far stay required, there's no safe universal default
/// for scene-scale numbers.
/// </summary>
public readonly record struct OrthographicCameraBundle(float Size, float Near, float Far, int Order = 0, bool ClearOnBegin = true) : IComponentBundle
{
    /// <inheritdoc/>
    public void ApplyTo<TSink>(TSink sink) where TSink : IComponentSink, allows ref struct =>
        new BundleBuilder<TSink>(sink).Add(new OrthographicCamera(Order, ClearOnBegin, Size, Near, Far));
}
