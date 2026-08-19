using System.Runtime.InteropServices;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Linear RGBA, each channel 0..1. Matches what the instance buffer writes directly into a
/// shader <c>float4</c>, no 0..255 conversion anywhere in this package. Explicit
/// <see cref="LayoutKind.Sequential"/> since this type gets copied byte-for-byte into raw GPU
/// memory (<see cref="SpriteInstanceData"/>, via <c>Buffer.MemoryCopy</c>), and the runtime's
/// default struct layout isn't guaranteed to preserve declaration order.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Color(float R, float G, float B, float A)
{
    /// <summary>Opaque white, the default tint. Leaves a texture's own colors unmodified.</summary>
    public static readonly Color White = new(1f, 1f, 1f, 1f);
}
