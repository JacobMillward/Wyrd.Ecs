namespace Wyrd.Ecs.Renderer;

/// <summary>
/// A loaded mesh's GPU-side identity: vertex/index buffers plus the local-space bounding
/// sphere computed once at load time from real vertex extents (see <see cref="MeshBounds.ComputeLocal"/>).
/// Never held directly by a component, always through a <see cref="Handle{T}"/>, same shape as
/// <see cref="Texture"/>, same reasoning. <c>internal</c> fields/constructor for the same
/// accessibility reason <see cref="Texture"/> documents.
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
