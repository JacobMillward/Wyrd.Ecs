using System.Numerics;
using System.Runtime.InteropServices;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Exact per-vertex layout uploaded into a <see cref="Mesh"/>'s GPU vertex buffer and read by
/// <c>UnlitMesh.vert.hlsl</c>'s <c>VertexInput</c>. 32 bytes: <see cref="Position"/> at offset
/// 0, <see cref="Normal"/> at offset 12, <see cref="UV"/> at offset 24; <c>CreateMeshPipeline</c>
/// hardcodes these offsets, so a field reorder here must update that too. <see cref="Normal"/>
/// is carried for a future lit shader; <c>UnlitMesh</c> doesn't read it, so the compiled shader
/// drops it from its own interface entirely.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MeshVertex(Vector3 Position, Vector3 Normal, Vector2 UV);
