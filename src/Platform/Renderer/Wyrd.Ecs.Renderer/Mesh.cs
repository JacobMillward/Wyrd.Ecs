namespace Wyrd.Ecs.Renderer;

/// <summary>
/// A loaded mesh's GPU-side identity: vertex/index buffers plus the local-space bounding
/// sphere computed once at load time from real vertex extents (see <see cref="MeshBounds.ComputeLocal"/>).
/// Never held directly by a component, always through a <see cref="Handle{T}"/>: async
/// load/unload can't invalidate an already-placed <see cref="MeshRenderer"/> this way.
/// <c>internal</c> fields/constructor: a consumer holds a <see cref="Handle{Mesh}"/> but can't
/// read or construct a <see cref="Mesh"/> directly. Public only because <see cref="Handle{T}"/>'s
/// type argument must be at least as accessible as <see cref="MeshRenderer"/>'s own public
/// <c>Mesh</c> property.
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
