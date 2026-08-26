using System.Numerics;
using System.Runtime.InteropServices;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Exact per-instance layout written into <see cref="InstanceBuffer{T}"/> and read by
/// <c>UnlitMesh.vert.hlsl</c> via <c>SV_InstanceID</c>. Smaller than <see cref="SpriteInstanceData"/>:
/// no source-rect, since a mesh's UVs come from its own vertex buffer, not a texture
/// sub-region. A distinct type rather than reusing <see cref="SpriteInstanceData"/> with an
/// unused field, since this is its own wire format for its own shader, not sprite's.
///
/// <see cref="FieldOffsetAttribute"/> on every field, matching <see cref="SpriteInstanceData"/>'s
/// own offsets for the same reason: HLSL's <c>StructuredBuffer</c> element packing aligns every
/// <c>float4</c> to a 16-byte boundary, so <see cref="Rotation"/> sits at 16 and
/// <see cref="Tint"/> at 48. <c>InstanceDataLayoutTests</c> (in this package's test project) pins
/// these offsets down.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
internal readonly struct MeshInstanceData
{
    [FieldOffset(0)] public readonly Vector3 Position;
    [FieldOffset(16)] public readonly Quaternion Rotation;
    [FieldOffset(32)] public readonly Vector3 Scale;
    [FieldOffset(48)] public readonly Color Tint;

    public MeshInstanceData(Vector3 position, Quaternion rotation, Vector3 scale, Color tint)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
        Tint = tint;
    }
}
