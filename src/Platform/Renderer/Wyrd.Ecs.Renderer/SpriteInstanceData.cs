using System.Numerics;
using System.Runtime.InteropServices;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Exact per-instance layout written into <see cref="InstanceBuffer{T}"/> and read by
/// <c>UnlitSprite.vert.hlsl</c> via <c>SV_InstanceID</c>. <see cref="SourceRectPixels"/> packs
/// <c>(X, Y, Width, Height)</c> as a <c>Vector4</c>, not <see cref="Rect"/>, since this struct's
/// layout must match the shader's structured-buffer element exactly, and <see cref="Rect"/> is
/// this package's public, nullable-friendly surface, not the wire format.
///
/// <see cref="FieldOffsetAttribute"/> on every field, matching HLSL's own <c>StructuredBuffer</c>
/// element packing: every <c>float4</c> aligns to a 16-byte boundary, so <see cref="Rotation"/>
/// sits at 16 and <see cref="Tint"/> at 48, each 4 bytes past where <see cref="LayoutKind.Sequential"/>
/// would tightly pack them. <c>InstanceDataLayoutTests</c> (in this package's test project)
/// pins these offsets down.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 80)]
internal readonly struct SpriteInstanceData
{
    [FieldOffset(0)] public readonly Vector3 Position;
    [FieldOffset(16)] public readonly Quaternion Rotation;
    [FieldOffset(32)] public readonly Vector3 Scale;
    [FieldOffset(48)] public readonly Color Tint;
    [FieldOffset(64)] public readonly Vector4 SourceRectPixels;

    public SpriteInstanceData(Vector3 position, Quaternion rotation, Vector3 scale, Color tint, Vector4 sourceRectPixels)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
        Tint = tint;
        SourceRectPixels = sourceRectPixels;
    }
}
