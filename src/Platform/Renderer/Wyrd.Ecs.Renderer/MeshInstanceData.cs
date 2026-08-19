using System.Numerics;
using System.Runtime.InteropServices;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Exact per-instance layout written into <see cref="InstanceBuffer{T}"/> and read by
/// <c>UnlitMesh.vert.hlsl</c> via <c>SV_InstanceID</c>. Smaller than <see cref="SpriteInstanceData"/>:
/// no source-rect, since a mesh's UVs come from its own vertex buffer, not a texture
/// sub-region. A distinct type rather than reusing <see cref="SpriteInstanceData"/> with an
/// unused field, since this is its own wire format for its own shader, not sprite's.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MeshInstanceData(Vector3 Position, Quaternion Rotation, Vector3 Scale, Color Tint);
