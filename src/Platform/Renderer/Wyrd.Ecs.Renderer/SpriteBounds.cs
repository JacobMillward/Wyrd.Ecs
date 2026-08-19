using System.Numerics;

namespace Wyrd.Ecs.Renderer;

/// <summary>World-space culling volume for one sprite this frame. Computed fresh each call: texture pixel dimensions are cheap to read from the already-resolved <see cref="Texture"/>, no separate cache needed on top of what <see cref="TextureArena"/> already holds.</summary>
internal readonly record struct BoundingSphere(Vector3 Center, float Radius);

/// <summary>
/// Sphere-based frustum culling: cheap (one dot-product-per-plane test), conservative (a
/// sphere over-approximates a rectangular sprite, never under-approximates it, so nothing
/// visible is ever wrongly culled). Bounds derive from the texture's pixel dimensions
/// (1 world unit = 1 pixel at unit <see cref="Wyrd.Ecs.Transform.Scale"/>, combined with the
/// entity's actual world scale and the sprite's <see cref="Sprite.SourceRect"/>), not a
/// separate authored size field.
/// </summary>
internal static class SpriteBounds
{
    public static BoundingSphere Compute(WorldTransform transform, Sprite sprite, int texturePixelWidth, int texturePixelHeight)
    {
        var (width, height) = sprite.SourceRect is { } rect ? (rect.Width, rect.Height) : (texturePixelWidth, texturePixelHeight);
        var halfDiagonal = new Vector2(width, height).Length() * 0.5f;
        var worldRadius = halfDiagonal * Math.Max(Math.Abs(transform.Scale.X), Math.Abs(transform.Scale.Y));
        return new BoundingSphere(transform.Position, worldRadius);
    }

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
