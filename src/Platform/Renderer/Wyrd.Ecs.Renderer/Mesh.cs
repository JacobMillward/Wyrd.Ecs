namespace Wyrd.Ecs.Renderer;

/// <summary>
/// A loaded mesh's GPU-side identity: vertex/index buffers plus the local-space bounding
/// sphere computed once at load time from real vertex extents (see <see cref="MeshBounds.ComputeLocal"/>).
/// Never held directly by a component, always through a <see cref="Handle{T}"/>, so async
/// load/unload can't invalidate an already-placed <see cref="MeshRenderer"/>. Fields and
/// constructor stay <c>internal</c>; the class itself is public only because
/// <see cref="Handle{T}"/>'s type argument must be at least as accessible as
/// <see cref="MeshRenderer"/>'s own public <c>Mesh</c> property.
/// </summary>
public sealed class Mesh
{
    internal readonly IntPtr GpuVertexBuffer;
    internal readonly IntPtr GpuIndexBuffer;
    internal readonly uint IndexCount;
    internal readonly BoundingSphere LocalBounds;

    internal Mesh(IntPtr gpuVertexBuffer, IntPtr gpuIndexBuffer, uint indexCount, BoundingSphere localBounds)
    {
        GpuVertexBuffer = gpuVertexBuffer;
        GpuIndexBuffer = gpuIndexBuffer;
        IndexCount = indexCount;
        LocalBounds = localBounds;
    }
}
