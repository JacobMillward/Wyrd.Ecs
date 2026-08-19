using System.Runtime.InteropServices;
using SDL3;
using StbImageSharp;

namespace Wyrd.Ecs.Renderer;

public sealed partial class RendererSystem
{
    private readonly TextureArena _textureArena = new();
    private readonly Dictionary<int, TaskCompletionSource> _textureLoadCompletions = new();

    /// <summary>
    /// Allocates a <see cref="Handle{T}"/> immediately (state <see cref="LoadState.Loading"/>)
    /// and starts a background decode. The GPU upload itself happens later, inside this
    /// system's existing copy pass (see <see cref="Execute"/>), since SDL_GPU device calls can
    /// only run on the thread that owns the device — this method never touches the device.
    /// </summary>
    public Handle<Texture> LoadTexture(string path)
    {
        var handle = _textureArena.Reserve(path);
        var completion = new TaskCompletionSource();
        _textureLoadCompletions[handle.Index] = completion;

        Task.Run(() =>
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var image = ImageResult.FromMemory(bytes, ColorComponents.RedGreenBlueAlpha);
                PendingUploads.Enqueue(copyPass => UploadDecoded(handle, image, copyPass, completion));
            }
            catch (Exception ex)
            {
                PendingUploads.Enqueue(_ =>
                {
                    _textureArena.MarkFailed(handle);
                    completion.TrySetException(ex);
                });
            }
        });

        return handle;
    }

    /// <summary>Runs on the render thread, inside the copy pass — creates the GPU texture and uploads decoded pixels via the transfer-buffer/staging pattern.</summary>
    private void UploadDecoded(Handle<Texture> handle, ImageResult image, IntPtr copyPass, TaskCompletionSource completion)
    {
        var textureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Usage = SDL.GPUTextureUsageFlags.Sampler,
            Width = (uint)image.Width,
            Height = (uint)image.Height,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            SampleCount = SDL.GPUSampleCount.SampleCount1,
        };
        var gpuTexture = SDL.CreateGPUTexture(Device, in textureCreateInfo);
        if (gpuTexture == IntPtr.Zero)
        {
            _textureArena.MarkFailed(handle);
            completion.TrySetException(new InvalidOperationException($"SDL_CreateGPUTexture failed: {SDL.GetError()}"));
            return;
        }

        var transferCreateInfo = new SDL.GPUTransferBufferCreateInfo
        {
            Usage = SDL.GPUTransferBufferUsage.Upload,
            Size = (uint)image.Data.Length,
        };
        var transferBuffer = SDL.CreateGPUTransferBuffer(Device, in transferCreateInfo);
        var mapped = SDL.MapGPUTransferBuffer(Device, transferBuffer, false);
        Marshal.Copy(image.Data, 0, mapped, image.Data.Length);
        SDL.UnmapGPUTransferBuffer(Device, transferBuffer);

        var source = new SDL.GPUTextureTransferInfo { TransferBuffer = transferBuffer, Offset = 0, PixelsPerRow = (uint)image.Width, RowsPerLayer = (uint)image.Height };
        var destination = new SDL.GPUTextureRegion { Texture = gpuTexture, MipLevel = 0, Layer = 0, X = 0, Y = 0, Z = 0, W = (uint)image.Width, H = (uint)image.Height, D = 1 };
        SDL.UploadToGPUTexture(copyPass, in source, in destination, false);
        SDL.ReleaseGPUTransferBuffer(Device, transferBuffer);

        _textureArena.MarkLoaded(handle, new Texture(gpuTexture, image.Width, image.Height));
        completion.TrySetResult();
    }

    /// <summary>Task that completes (or faults with the captured decode/IO/GPU exception) once <paramref name="handle"/> resolves. Polling <see cref="GetTextureLoadState"/> instead avoids the throw for call sites that don't want to await.</summary>
    public Task WaitForLoad(Handle<Texture> handle) => _textureLoadCompletions[handle.Index].Task;

    internal LoadState GetTextureLoadState(Handle<Texture> handle) => _textureArena.GetState(handle);

    /// <summary>Decrements the handle's use-count; once it reaches zero, the GPU texture is queued on <see cref="DeferredDestroy"/>, released only after <see cref="FrameInFlightTracker.FramesInFlight"/> further frames — never while a command buffer that could still reference it might be in flight.</summary>
    public void Unload(Handle<Texture> handle)
    {
        if (!_textureArena.Unload(handle, out var texture) || texture is null) return;

        var gpuTexture = texture.GpuTexture;
        var device = Device;
        DeferredDestroy.Enqueue(FrameInFlight.CurrentFrame, () => SDL.ReleaseGPUTexture(device, gpuTexture));
    }
}
