using System.Numerics;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Queried as <c>(Transform, OrthographicCamera)</c>. An <see cref="OrthographicCamera"/> entity
/// with no <see cref="Transform"/> simply never matches and is never rendered (structural, not a
/// runtime check). Active cameras (this and <see cref="PerspectiveCamera"/> alike) render in
/// <see cref="Order"/> sequence into the same swapchain target; a 3D-scene-plus-2D-HUD frame is a
/// <see cref="PerspectiveCamera"/> plus an <see cref="OrthographicCamera"/> entity with different
/// <see cref="Order"/>/<see cref="ClearOnBegin"/>, no separate compositing step.
/// </summary>
public readonly record struct OrthographicCamera(int Order, bool ClearOnBegin, float Size, float Near, float Far) : IComponent
{
    /// <summary>The matrix that moves world-space points into view space. See <see cref="CameraMath.GetViewMatrix"/>.</summary>
    public Matrix4x4 GetViewMatrix(WorldTransform transform) => CameraMath.GetViewMatrix(transform);

    /// <summary>View-space to clip-space matrix, left-handed to match <see cref="GetViewMatrix"/> (see its doc comment). <see cref="Size"/> is half the vertical extent, matching Unity's <c>Camera.orthographicSize</c>.</summary>
    public Matrix4x4 GetProjectionMatrix(float aspectRatio) =>
        Matrix4x4.CreateOrthographicLeftHanded(Size * 2f * aspectRatio, Size * 2f, Near, Far);

    /// <summary>
    /// Inverse-projects a screen-space point (pixels, origin top-left, <c>Z</c> = depth on a
    /// fixed plane) back to world space. Built from the same matrices <see cref="GetViewMatrix"/>/
    /// <see cref="GetProjectionMatrix"/> compute for rendering, matching Unity's
    /// <c>Camera.ScreenToWorldPoint(Vector3)</c> signature.
    /// </summary>
    public Vector3 ScreenToWorld(WorldTransform transform, float aspectRatio, Vector2 viewportSize, Vector3 screenPoint)
    {
        var (invView, invProjection, ndc) = CameraMath.PrepareScreenToWorld(transform, GetProjectionMatrix(aspectRatio), viewportSize, screenPoint);
        var viewPos = Vector3.Transform(new Vector3(ndc.X, ndc.Y, 0f), invProjection);
        viewPos.Z = -screenPoint.Z;
        return Vector3.Transform(viewPos, invView);
    }

    /// <summary>Projects a world-space point to screen space (pixels, origin top-left). Inverse of <see cref="ScreenToWorld"/> for the same inputs.</summary>
    public Vector2 WorldToScreen(WorldTransform transform, float aspectRatio, Vector2 viewportSize, Vector3 worldPoint) =>
        CameraMath.WorldToScreen(transform, GetProjectionMatrix(aspectRatio), viewportSize, worldPoint);
}
