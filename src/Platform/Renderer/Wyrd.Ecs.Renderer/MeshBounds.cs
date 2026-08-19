using System.Numerics;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Mesh culling bounds, computed in two stages since a mesh's local bounds don't depend on any
/// per-entity data: <see cref="ComputeLocal"/> once at load time from real vertex extents (an
/// axis-aligned box collapsed to a sphere, over-approximating a non-cubic mesh but never
/// under-approximating it), <see cref="ComputeWorld"/> every frame per entity, combining that
/// fixed local sphere with the entity's live <see cref="WorldTransform"/>.
/// </summary>
internal static class MeshBounds
{
    public static BoundingSphere ComputeLocal(ReadOnlySpan<MeshVertex> vertices)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var vertex in vertices)
        {
            min = Vector3.Min(min, vertex.Position);
            max = Vector3.Max(max, vertex.Position);
        }
        var center = (min + max) * 0.5f;
        var radius = Vector3.Distance(min, max) * 0.5f;
        return new BoundingSphere(center, radius);
    }

    /// <summary>Conservative under non-uniform scale: radius scales by the largest single axis, matching <see cref="SpriteBounds"/>'s own non-uniform-scale handling.</summary>
    public static BoundingSphere ComputeWorld(WorldTransform transform, BoundingSphere localBounds)
    {
        var scaledCenter = localBounds.Center * transform.Scale;
        var worldCenter = transform.Position + Vector3.Transform(scaledCenter, transform.Rotation);
        var maxScale = Math.Max(Math.Abs(transform.Scale.X), Math.Max(Math.Abs(transform.Scale.Y), Math.Abs(transform.Scale.Z)));
        return new BoundingSphere(worldCenter, localBounds.Radius * maxScale);
    }
}
