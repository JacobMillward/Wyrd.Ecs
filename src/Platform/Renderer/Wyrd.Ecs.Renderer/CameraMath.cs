using System.Numerics;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// View/projection math shared between <see cref="PerspectiveCamera"/> and
/// <see cref="OrthographicCamera"/>. Not public: each camera type exposes its own
/// <c>GetViewMatrix</c>/<c>WorldToScreen</c>/<c>ScreenToWorld</c>, so callers never need to know
/// this helper exists: only the mode-specific pieces (<c>GetProjectionMatrix</c>, the
/// NDC-to-view-space reconstruction inside <c>ScreenToWorld</c>) live on the camera types
/// themselves.
/// </summary>
internal static class CameraMath
{
    /// <summary>
    /// The matrix that moves world-space points into view space, built from position and
    /// rotation only. Deliberately ignores <see cref="WorldTransform.Scale"/> (a scaled camera
    /// isn't a meaningful concept in any engine's camera math) and never calls
    /// <see cref="Matrix4x4.Invert(Matrix4x4, out Matrix4x4)"/>: composing the inverse
    /// translation and inverse (conjugate) rotation directly is both cheaper than a general 4x4
    /// invert and, unlike inverting a matrix that could carry a degenerate scale (a
    /// default-constructed, non-<see cref="Wyrd.Ecs.Transform.Identity"/> <c>Transform</c> has
    /// <c>Scale</c> zeroed, see that type's doc comment), never singular. Produces left-handed
    /// view space (forward = +Z for an identity-rotated camera), matching Unity's convention.
    /// Each camera type's <c>GetProjectionMatrix</c> uses the matching <c>LeftHanded</c>
    /// constructor deliberately; mixing handedness silently culls everything a camera looks at.
    /// </summary>
    public static Matrix4x4 GetViewMatrix(WorldTransform transform)
    {
        var rotationInverse = Matrix4x4.CreateFromQuaternion(Quaternion.Conjugate(transform.Rotation));
        var translationInverse = Matrix4x4.CreateTranslation(-transform.Position);
        return translationInverse * rotationInverse;
    }

    /// <summary>Screen-space (pixels, origin top-left) to normalized device coordinates; Z passed through untouched.</summary>
    public static Vector3 NdcFromScreen(Vector2 viewportSize, Vector3 screenPoint) => new(
        screenPoint.X / viewportSize.X * 2f - 1f,
        1f - screenPoint.Y / viewportSize.Y * 2f,
        screenPoint.Z);

    /// <summary>Projects a world-space point to screen space (pixels, origin top-left). Mode-agnostic once given a projection matrix.</summary>
    public static Vector2 WorldToScreen(WorldTransform transform, Matrix4x4 projection, Vector2 viewportSize, Vector3 worldPoint)
    {
        var view = GetViewMatrix(transform);
        var clip = Vector3.Transform(worldPoint, view * projection);
        return new Vector2(
            (clip.X + 1f) * 0.5f * viewportSize.X,
            (1f - clip.Y) * 0.5f * viewportSize.Y);
    }

    /// <summary>
    /// The inverse view/projection matrices and screen-to-NDC conversion every
    /// <c>ScreenToWorld</c> needs before applying its own mode-specific NDC-to-view-space
    /// reconstruction (fixed depth plane for <see cref="OrthographicCamera"/>, ray direction for
    /// <see cref="PerspectiveCamera"/>).
    /// </summary>
    public static (Matrix4x4 InvView, Matrix4x4 InvProjection, Vector3 Ndc) PrepareScreenToWorld(WorldTransform transform, Matrix4x4 projection, Vector2 viewportSize, Vector3 screenPoint)
    {
        Matrix4x4.Invert(GetViewMatrix(transform), out var invView);
        Matrix4x4.Invert(projection, out var invProjection);
        return (invView, invProjection, NdcFromScreen(viewportSize, screenPoint));
    }
}
