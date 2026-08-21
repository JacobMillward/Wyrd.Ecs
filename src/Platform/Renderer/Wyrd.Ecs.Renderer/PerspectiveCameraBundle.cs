namespace Wyrd.Ecs.Renderer;

/// <summary>
/// A <see cref="PerspectiveCamera"/> entity in one call. <see cref="FieldOfView"/> is typed as
/// <see cref="Angle"/> so a caller can't accidentally pass a world-space extent where an angle is
/// meant. <see cref="Angle"/>'s wraparound is correct for a rotation but not for a FOV, which has
/// no wraparound meaning of its own: a value outside (0, 180) degrees produces a degenerate
/// projection matrix, not validated here. `Order`/`ClearOnBegin` default to the common
/// single-camera case (0, true); `FieldOfView`/near/far stay required, there's no safe universal
/// default for scene-scale numbers.
/// </summary>
public readonly record struct PerspectiveCameraBundle(Angle FieldOfView, float Near, float Far, int Order = 0, bool ClearOnBegin = true) : IComponentBundle
{
    /// <inheritdoc/>
    public void ApplyTo<TSink>(TSink sink) where TSink : IComponentSink, allows ref struct =>
        new BundleBuilder<TSink>(sink).Add(new PerspectiveCamera(Order, ClearOnBegin, FieldOfView, Near, Far));
}
