using System.Numerics;

namespace Wyrd.Ecs.Renderer;

/// <summary>World-space culling volume, shared by <see cref="SpriteBounds"/> and <see cref="MeshBounds"/>.</summary>
internal readonly record struct BoundingSphere(Vector3 Center, float Radius);

/// <summary>
/// Sphere-based frustum culling: cheap (one dot-product-per-plane test), conservative (a
/// sphere over-approximates any convex shape, never under-approximates it, so nothing visible
/// is ever wrongly culled). Not specific to sprites or meshes: both drawable kinds resolve
/// their own <see cref="BoundingSphere"/> and share this one test.
/// </summary>
internal static class FrustumCulling
{
    public static bool IsInsideFrustum(BoundingSphere bounds, Matrix4x4 viewProjection)
    {
        Span<Vector4> planes = stackalloc Vector4[6];
        var m = viewProjection;
        planes[0] = new Vector4(m.M14 + m.M11, m.M24 + m.M21, m.M34 + m.M31, m.M44 + m.M41); // left
        planes[1] = new Vector4(m.M14 - m.M11, m.M24 - m.M21, m.M34 - m.M31, m.M44 - m.M41); // right
        planes[2] = new Vector4(m.M14 + m.M12, m.M24 + m.M22, m.M34 + m.M32, m.M44 + m.M42); // bottom
        planes[3] = new Vector4(m.M14 - m.M12, m.M24 - m.M22, m.M34 - m.M32, m.M44 - m.M42); // top
        planes[4] = new Vector4(m.M13, m.M23, m.M33, m.M43);                                  // near
        planes[5] = new Vector4(m.M14 - m.M13, m.M24 - m.M23, m.M34 - m.M33, m.M44 - m.M43); // far

        foreach (var plane in planes)
        {
            var normal = new Vector3(plane.X, plane.Y, plane.Z);
            var length = normal.Length();
            if (length < 1e-6f) continue;
            var distance = (Vector3.Dot(normal, bounds.Center) + plane.W) / length;
            if (distance < -bounds.Radius) return false;
        }

        return true;
    }
}
