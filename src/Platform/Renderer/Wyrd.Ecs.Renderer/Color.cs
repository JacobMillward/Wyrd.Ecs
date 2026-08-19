namespace Wyrd.Ecs.Renderer;

/// <summary>Linear RGBA, each channel 0..1 — matches what the instance buffer writes directly into a shader <c>float4</c>, no 0..255 conversion anywhere in this package.</summary>
public readonly record struct Color(float R, float G, float B, float A)
{
    /// <summary>Opaque white — the default tint, leaves a texture's own colors unmodified.</summary>
    public static readonly Color White = new(1f, 1f, 1f, 1f);
}
