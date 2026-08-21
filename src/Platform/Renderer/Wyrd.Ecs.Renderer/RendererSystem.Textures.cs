using System.Runtime.InteropServices;
using SDL3;
using StbImageSharp;
using Wyrd.Ecs.Assets;

namespace Wyrd.Ecs.Renderer;

public sealed partial class RendererSystem
{
    private readonly AssetArena<string, Texture> _textureArena = new();

    /// <summary>
    /// Allocates a <see cref="Handle{T}"/> immediately (state <see cref="LoadState.Loading"/>)
    /// and starts a background decode. Calling this again with a path already reserved returns
    /// the existing handle without re-decoding or re-uploading. <c>AssetArena.Reserve</c>'s
    /// <c>isNew</c> out-param is what makes this safe: without checking it, every repeat call
    /// would decode and GPU-upload the file again, leaking the previous <c>SDL_GPUTexture</c>.
    /// The GPU upload itself happens later, inside this system's existing copy pass (see <see
    /// cref="Execute"/>), since SDL_GPU device calls can only run on the thread that owns the
    /// device, so this method never touches the device.
    /// </summary>
    public Handle<Texture> LoadTexture(string path)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        var handle = _textureArena.Reserve(path, out var isNew);
        if (!isNew) return handle;

        Task.Run(() =>
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var image = ImageResult.FromMemory(bytes, ColorComponents.RedGreenBlueAlpha);
                PendingUploads.Enqueue(copyPass => UploadDecoded(handle, image, copyPass));
            }
            catch (Exception ex)
            {
                PendingUploads.Enqueue(_ => _textureArena.MarkFailed(handle, ex));
            }
        });

        return handle;
    }

    /// <summary>Runs on the render thread, inside the copy pass. Creates the GPU texture and uploads decoded pixels via the transfer-buffer/staging pattern.</summary>
    private void UploadDecoded(Handle<Texture> handle, ImageResult image, IntPtr copyPass)
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
            _textureArena.MarkFailed(handle, new InvalidOperationException($"SDL_CreateGPUTexture failed: {SDL.GetError()}"));
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
    }

    /// <summary>Task that completes (or faults with the captured decode/IO/GPU exception) once <paramref name="handle"/> resolves. Polling <see cref="GetTextureLoadState"/> instead avoids the throw for call sites that don't want to await.</summary>
    public Task WaitForLoadAsync(Handle<Texture> handle)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        return _textureArena.WaitForLoadAsync(handle);
    }

    internal LoadState GetTextureLoadState(Handle<Texture> handle) => _textureArena.GetState(handle);

    /// <summary>Decrements the handle's use-count; once it reaches zero, the GPU texture is queued on <see cref="DeferredDestroyQueue"/>, released only after <see cref="FrameInFlightTracker.FramesInFlight"/> further frames, never while a command buffer that could still reference it might be in flight.</summary>
    public void Unload(Handle<Texture> handle)
    {
        ObjectDisposedException.ThrowIf(_destroyed, this);
        if (!_textureArena.Unload(handle, out var texture) || texture is null) return;

        var gpuTexture = texture.GpuTexture;
        var device = Device;
        DeferredDestroy.Enqueue(FrameInFlight.CurrentFrame, () => SDL.ReleaseGPUTexture(device, gpuTexture));
    }

    /// <summary>
    /// A 2x2 magenta/black checkerboard, uploaded synchronously at construction (not through
    /// the async path, since it must exist before the first tick, and it's tiny/local, not a
    /// file load). Drawn in place of any <see cref="Handle{T}"/> still <see cref="LoadState.Loading"/>
    /// or gone <see cref="LoadState.Failed"/>, so a broken asset reference looks wrong on
    /// screen instead of silently disappearing.
    /// </summary>
    internal Texture PlaceholderTexture { get; }

    private Texture CreatePlaceholderTexture()
    {
        var textureCreateInfo = new SDL.GPUTextureCreateInfo
        {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8G8B8A8Unorm,
            Usage = SDL.GPUTextureUsageFlags.Sampler,
            Width = 2,
            Height = 2,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            SampleCount = SDL.GPUSampleCount.SampleCount1,
        };
        var gpuTexture = SDL.CreateGPUTexture(Device, in textureCreateInfo);
        if (gpuTexture == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUTexture (placeholder) failed: {SDL.GetError()}");

        byte[] pixels = [255, 0, 255, 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 0, 255, 255]; // magenta, black, black, magenta

        var transferCreateInfo = new SDL.GPUTransferBufferCreateInfo { Usage = SDL.GPUTransferBufferUsage.Upload, Size = (uint)pixels.Length };
        var transferBuffer = SDL.CreateGPUTransferBuffer(Device, in transferCreateInfo);
        var mapped = SDL.MapGPUTransferBuffer(Device, transferBuffer, false);
        Marshal.Copy(pixels, 0, mapped, pixels.Length);
        SDL.UnmapGPUTransferBuffer(Device, transferBuffer);

        var commandBuffer = SDL.AcquireGPUCommandBuffer(Device);
        var copyPass = SDL.BeginGPUCopyPass(commandBuffer);
        var source = new SDL.GPUTextureTransferInfo { TransferBuffer = transferBuffer, Offset = 0, PixelsPerRow = 2, RowsPerLayer = 2 };
        var destination = new SDL.GPUTextureRegion { Texture = gpuTexture, MipLevel = 0, Layer = 0, X = 0, Y = 0, Z = 0, W = 2, H = 2, D = 1 };
        SDL.UploadToGPUTexture(copyPass, in source, in destination, false);
        SDL.EndGPUCopyPass(copyPass);
        SDL.SubmitGPUCommandBuffer(commandBuffer);
        SDL.ReleaseGPUTransferBuffer(Device, transferBuffer);

        return new Texture(gpuTexture, 2, 2);
    }
}
