namespace Wyrd.Ecs.Renderer;

/// <summary>
/// A <see cref="Camera"/> entity in one call. `Order`/`ClearOnBegin` default to the common
/// single-camera case (0, true), `ProjectionMode`/FOV/near/far stay required, there's no safe
/// universal default for scene-scale numbers.
/// </summary>
public readonly record struct CameraBundle(ProjectionMode ProjectionMode, float FieldOfViewOrOrthographicSize, float Near, float Far, int Order = 0, bool ClearOnBegin = true) : IComponentBundle
{
    /// <inheritdoc/>
    public void ApplyTo<TSink>(TSink sink) where TSink : IComponentSink, allows ref struct =>
        new BundleBuilder<TSink>(sink).Add(new Camera(Order, ProjectionMode, ClearOnBegin, FieldOfViewOrOrthographicSize, Near, Far));
}
