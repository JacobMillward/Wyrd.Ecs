namespace Wyrd.Ecs.Renderer;

/// <summary>
/// A loaded texture's GPU-side identity. Never held directly by a component, always through a
/// <see cref="Handle{T}"/>, so async load/unload can't invalidate an already-placed
/// <see cref="Material"/>. Public only because <see cref="Handle{T}"/>'s type argument must be
/// at least as accessible as <see cref="Material"/>'s own public <c>Texture</c> property (a
/// mechanical C# accessibility rule, not an intent to expose this type's contents). Its
/// fields and constructor stay <c>internal</c>, so a consumer can hold a
/// <see cref="Handle{Texture}"/> but can't read or construct a <see cref="Texture"/> itself.
/// </summary>
public sealed class Texture
{
    internal readonly IntPtr GpuTexture;
    internal readonly int PixelWidth;
    internal readonly int PixelHeight;

    internal Texture(IntPtr gpuTexture, int pixelWidth, int pixelHeight)
    {
        GpuTexture = gpuTexture;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
    }
}
