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

    /// <summary>A <see cref="ProjectionMode.Perspective"/> camera. <paramref name="fieldOfView"/> is typed so a caller can't accidentally pass a world-space size where an angle is meant, unlike this record's own constructor.</summary>
    public static CameraBundle Perspective(Angle fieldOfView, float near, float far, int order = 0, bool clearOnBegin = true) =>
        new(ProjectionMode.Perspective, fieldOfView.Radians, near, far, order, clearOnBegin);

    /// <summary>A <see cref="ProjectionMode.Orthographic"/> camera. <paramref name="halfHeight"/> is half the vertical world-space extent, not an angle.</summary>
    public static CameraBundle Orthographic(float halfHeight, float near, float far, int order = 0, bool clearOnBegin = true) =>
        new(ProjectionMode.Orthographic, halfHeight, near, far, order, clearOnBegin);
}
