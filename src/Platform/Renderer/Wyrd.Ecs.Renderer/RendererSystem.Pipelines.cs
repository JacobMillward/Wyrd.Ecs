using SDL3;

namespace Wyrd.Ecs.Renderer;

public sealed partial class RendererSystem
{
    private static readonly SDL.GPUTextureFormat[] DepthStencilFormatPriority =
    [
        SDL.GPUTextureFormat.D32Float,
        SDL.GPUTextureFormat.D24UnormS8Uint,
        SDL.GPUTextureFormat.D16Unorm,
    ];

    /// <summary>
    /// The depth-stencil format every camera's depth texture and every pipeline's
    /// <see cref="SDL.GPUGraphicsPipelineTargetInfo.DepthStencilFormat"/> uses. Chosen once at
    /// construction: neither <see cref="SDL.GPUTextureFormat.D32Float"/> nor
    /// <see cref="SDL.GPUTextureFormat.D24UnormS8Uint"/> is guaranteed across drivers/hardware
    /// (SDL's own docs: always query before using either), so this queries
    /// <see cref="SDL.GPUTextureSupportsFormat"/> in priority order and falls back to the
    /// broadly-supported <see cref="SDL.GPUTextureFormat.D16Unorm"/>.
    /// </summary>
    internal SDL.GPUTextureFormat DepthStencilFormat { get; private set; }

    private static SDL.GPUTextureFormat ChooseDepthStencilFormat(IntPtr device)
    {
        foreach (var format in DepthStencilFormatPriority)
        {
            if (SDL.GPUTextureSupportsFormat(device, format, SDL.GPUTextureType.TextureType2D, SDL.GPUTextureUsageFlags.DepthStencilTarget))
                return format;
        }
        throw new InvalidOperationException("No supported depth-stencil texture format available for this device.");
    }
}
