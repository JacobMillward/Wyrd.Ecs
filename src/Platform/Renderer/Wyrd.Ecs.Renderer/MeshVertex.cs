using System.Numerics;
using System.Runtime.InteropServices;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Exact per-vertex layout uploaded into a <see cref="Mesh"/>'s GPU vertex buffer and read by
/// <c>UnlitMesh.vert.hlsl</c>'s <c>VertexInput</c>. 32 bytes: <see cref="Position"/> at offset
/// 0, <see cref="Normal"/> at offset 12, <see cref="UV"/> at offset 24. <c>CreateMeshPipeline</c>
/// hardcodes these offsets in its <c>GPUVertexAttribute</c> array, so a field reorder here must
/// update that array too. <see cref="Normal"/> is carried even though <c>UnlitMesh</c> doesn't
/// light anything yet: an unused vertex input gets dropped from the compiled shader's own
/// interface, so the pipeline simply omits a vertex-attribute entry for it. Kept for a future
/// lit shader, not read by anything in this phase.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MeshVertex(Vector3 Position, Vector3 Normal, Vector2 UV);
