using System.Numerics;

namespace Wyrd.Ecs.Renderer;

/// <summary>How a <see cref="Camera"/> projects view space to clip space.</summary>
public enum ProjectionMode
{
    /// <summary>Parallel projection — no perspective foreshortening. The 2D sprite path's camera.</summary>
    Orthographic,

    /// <summary>Perspective projection. Phase 4's 3D mesh path.</summary>
    Perspective,
}

/// <summary>
/// Queried as <c>(Transform, Camera)</c> — a <see cref="Camera"/> entity with no
/// <see cref="Transform"/> simply never matches and is never rendered (structural, not a
/// runtime check). Active cameras render in <see cref="Order"/> sequence into the same
/// swapchain target; a 3D-scene-plus-2D-HUD frame is two camera entities with different
/// <see cref="Order"/>/<see cref="ProjectionMode"/>/<see cref="ClearOnBegin"/> — no separate
/// compositing step. <see cref="FieldOfViewOrOrthographicSize"/> is vertical FOV in radians
/// for <see cref="ProjectionMode.Perspective"/>, or half the vertical world-space extent for
/// <see cref="ProjectionMode.Orthographic"/>.
/// </summary>
public readonly record struct Camera(
    int Order,
    ProjectionMode ProjectionMode,
    bool ClearOnBegin,
    float FieldOfViewOrOrthographicSize,
    float Near,
    float Far) : IComponent
{
    /// <summary>
    /// The matrix that moves world-space points into view space, built from position and
    /// rotation only — deliberately ignores <see cref="WorldTransform.Scale"/> (a scaled
    /// camera isn't a meaningful concept in any engine's camera math) and never calls
    /// <see cref="Matrix4x4.Invert(Matrix4x4, out Matrix4x4)"/>: composing the inverse
    /// translation and inverse (conjugate) rotation directly is both cheaper than a general
    /// 4x4 invert and, unlike inverting a matrix that could carry a degenerate scale (a
    /// default-constructed, non-<see cref="Wyrd.Ecs.Transform.Identity"/> <c>Transform</c> has
    /// <c>Scale</c> zeroed — see that type's doc comment), never singular.
    /// </summary>
    public Matrix4x4 GetViewMatrix(WorldTransform transform)
    {
        var rotationInverse = Matrix4x4.CreateFromQuaternion(Quaternion.Conjugate(transform.Rotation));
        var translationInverse = Matrix4x4.CreateTranslation(-transform.Position);
        return translationInverse * rotationInverse;
    }

    /// <summary>
    /// View-space to clip-space matrix for this camera's <see cref="ProjectionMode"/>. Uses
    /// the <c>LeftHanded</c> constructors deliberately — <see cref="System.Numerics"/>'s
    /// unsuffixed <c>CreateOrthographic</c>/<c>CreatePerspectiveFieldOfView</c> are
    /// right-handed (forward = -Z), but <see cref="GetViewMatrix"/>'s plain "invert the
    /// camera's world transform" arithmetic is handedness-neutral and naturally produces
    /// left-handed view space (forward = +Z for an identity-rotated camera) — matching
    /// Unity's convention, which this type's <see cref="ScreenToWorld"/> already aligns with.
    /// Mixing the two (confirmed empirically, not just reasoned) silently culls everything a
    /// camera looks at: a target placed in front of the camera along +Z comes out at negative
    /// view-space Z under the right-handed projection, landing outside the near/far planes.
    /// </summary>
    public Matrix4x4 GetProjectionMatrix(float aspectRatio) => ProjectionMode switch
    {
        ProjectionMode.Orthographic => Matrix4x4.CreateOrthographicLeftHanded(
            FieldOfViewOrOrthographicSize * 2f * aspectRatio, FieldOfViewOrOrthographicSize * 2f, Near, Far),
        ProjectionMode.Perspective => Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(
            FieldOfViewOrOrthographicSize, aspectRatio, Near, Far),
        _ => throw new ArgumentOutOfRangeException(nameof(ProjectionMode)),
    };

    /// <summary>
    /// Inverse-projects a screen-space point (pixels, origin top-left, <c>Z</c> = depth: a
    /// fixed plane for <see cref="ProjectionMode.Orthographic"/>, distance along the view ray
    /// for <see cref="ProjectionMode.Perspective"/>) back to world space. Built from the same
    /// matrices <see cref="GetViewMatrix"/>/<see cref="GetProjectionMatrix"/> compute for
    /// rendering, matching Unity's <c>Camera.ScreenToWorldPoint(Vector3)</c> signature.
    /// </summary>
    public Vector3 ScreenToWorld(WorldTransform transform, float aspectRatio, Vector2 viewportSize, Vector3 screenPoint)
    {
        var ndc = new Vector3(
            screenPoint.X / viewportSize.X * 2f - 1f,
            1f - screenPoint.Y / viewportSize.Y * 2f,
            screenPoint.Z);

        var view = GetViewMatrix(transform);
        Matrix4x4.Invert(view, out var invView);
        var projection = GetProjectionMatrix(aspectRatio);
        Matrix4x4.Invert(projection, out var invProjection);

        if (ProjectionMode == ProjectionMode.Orthographic)
        {
            var viewPos = Vector3.Transform(new Vector3(ndc.X, ndc.Y, 0f), invProjection);
            viewPos.Z = -screenPoint.Z;
            return Vector3.Transform(viewPos, invView);
        }

        var viewRayPoint = Vector3.Transform(new Vector3(ndc.X, ndc.Y, 1f), invProjection);
        var viewRayDir = Vector3.Normalize(viewRayPoint);
        return Vector3.Transform(viewRayDir * screenPoint.Z, invView);
    }

    /// <summary>Projects a world-space point to screen space (pixels, origin top-left). Inverse of <see cref="ScreenToWorld"/> for the same inputs.</summary>
    public Vector2 WorldToScreen(WorldTransform transform, float aspectRatio, Vector2 viewportSize, Vector3 worldPoint)
    {
        var view = GetViewMatrix(transform);
        var projection = GetProjectionMatrix(aspectRatio);
        var clip = Vector3.Transform(worldPoint, view * projection);

        return new Vector2(
            (clip.X + 1f) * 0.5f * viewportSize.X,
            (1f - clip.Y) * 0.5f * viewportSize.Y);
    }
}
