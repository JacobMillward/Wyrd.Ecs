using System.Numerics;
using System.Runtime.InteropServices;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Exact per-instance layout written into <see cref="InstanceBuffer"/> and read by
/// <c>UnlitSprite.vert.hlsl</c> via <c>SV_InstanceID</c>. <see cref="SourceRectPixels"/> packs
/// <c>(X, Y, Width, Height)</c> — <c>Vector4</c>, not <see cref="Rect"/>, since this struct's
/// layout must match the shader's structured-buffer element exactly, and <see cref="Rect"/> is
/// this package's public, nullable-friendly surface, not the wire format.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SpriteInstanceData(Vector3 Position, Quaternion Rotation, Vector3 Scale, Color Tint, Vector4 SourceRectPixels);
