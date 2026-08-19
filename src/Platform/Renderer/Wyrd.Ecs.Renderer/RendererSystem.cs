using SDL3;
using Wyrd.Ecs.Platform;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Owns the SDL_GPU device's lifecycle and the per-frame render loop. A hand-written
/// <see cref="EcsSystem"/>, not a <see cref="QuerySystem"/>, since device claim/release and
/// swapchain handling are exactly the manual control flow <see cref="EcsSystem"/> exists for.
/// Runs at Variable cadence (once per <see cref="World.Update"/>): rendering is presentation,
/// deliberately decoupled from simulation rate.
/// </summary>
public sealed partial class RendererSystem : EcsSystem
{
    private readonly PlatformSystem _platform;
    private readonly PendingUploadQueue _pendingUploads = new();
    private readonly DeferredDestroyQueue _deferredDestroy = new();

    /// <summary>The native <c>SDL_GPUDevice*</c>, for consumers that need direct SDL3-CS access (the escape hatch).</summary>
    public IntPtr Device { get; }

    /// <summary>Tracks the current frame-in-flight slot; shared by later phases' per-frame buffers.</summary>
    public FrameInFlightTracker FrameInFlight { get; } = new();

    /// <summary>Queue for handing decoded asset data to the GPU upload step. Populated by later phases (asset loading).</summary>
    internal PendingUploadQueue PendingUploads => _pendingUploads;

    /// <summary>Queue for delaying a GPU resource's release until it's safe. Populated by later phases (asset unloading).</summary>
    internal DeferredDestroyQueue DeferredDestroy => _deferredDestroy;

    /// <summary>
    /// Creates a GPU device requesting every desktop shader format (SPIR-V/DXIL/MSL) and
    /// claims <paramref name="platform"/>'s window for it. Throws
    /// <see cref="InvalidOperationException"/> if either fails, wrapping <c>SDL_GetError()</c>.
    /// </summary>
    public RendererSystem(World world, PlatformSystem platform)
    {
        _platform = platform;

        Device = SDL.CreateGPUDevice(
            SDL.GPUShaderFormat.SPIRV | SDL.GPUShaderFormat.DXIL | SDL.GPUShaderFormat.MSL,
            debugMode: true,
            name: null);
        if (Device == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_CreateGPUDevice failed: {SDL.GetError()}");

        if (!SDL.ClaimWindowForGPUDevice(Device, platform.Window))
        {
            var error = SDL.GetError();
            SDL.DestroyGPUDevice(Device);
            throw new InvalidOperationException($"SDL_ClaimWindowForGPUDevice failed: {error}");
        }

        PlaceholderTexture = CreatePlaceholderTexture();
        PlaceholderMesh = CreatePlaceholderMesh();
        CreateSpritePipeline();
        CreateMeshPipeline();
    }

    /// <inheritdoc/>
    protected override void Execute(World world, Time time)
    {
        var commandBuffer = SDL.AcquireGPUCommandBuffer(Device);
        if (commandBuffer == IntPtr.Zero)
            throw new InvalidOperationException($"SDL_AcquireGPUCommandBuffer failed: {SDL.GetError()}");

        // Uploads always run, even if the swapchain acquire below is skipped this tick,
        // since async asset loads (later phases) must keep making progress regardless of
        // swapchain/present state.
        var copyPass = SDL.BeginGPUCopyPass(commandBuffer);
        _pendingUploads.DrainInto(copyPass);
        SDL.EndGPUCopyPass(copyPass);

        _deferredDestroy.DrainReady(FrameInFlight.CurrentFrame, FrameInFlightTracker.FramesInFlight);

        if (!SDL.AcquireGPUSwapchainTexture(commandBuffer, _platform.Window, out var swapchainTexture, out _, out _))
            throw new InvalidOperationException($"SDL_AcquireGPUSwapchainTexture failed: {SDL.GetError()}");

        // A null swapchain texture here is a real, non-error SDL_GPU state (minimized window,
        // too many frames in flight, mid-resize); skip presenting this tick and retry next.
        if (swapchainTexture != IntPtr.Zero)
        {
            SDL.GetWindowSizeInPixels(_platform.Window, out var viewportWidth, out var viewportHeight);
            DrawFrame(world, commandBuffer, swapchainTexture, viewportWidth, viewportHeight);
        }

        if (!SDL.SubmitGPUCommandBuffer(commandBuffer))
            throw new InvalidOperationException($"SDL_SubmitGPUCommandBuffer failed: {SDL.GetError()}");

        FrameInFlight.Advance();
    }

    /// <inheritdoc/>
    protected override void OnDestroy()
    {
        SDL.ReleaseGPUGraphicsPipeline(Device, SpritePipeline);
        SDL.ReleaseGPUSampler(Device, SpriteSampler);
        SDL.ReleaseGPUGraphicsPipeline(Device, MeshPipeline);
        SDL.ReleaseGPUSampler(Device, MeshSampler);
        SDL.ReleaseWindowFromGPUDevice(Device, _platform.Window);
        SDL.DestroyGPUDevice(Device);
    }
}
