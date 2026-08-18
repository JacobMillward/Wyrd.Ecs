using System.Collections.Concurrent;

namespace Wyrd.Ecs.Renderer;

/// <summary>
/// Hands decoded asset data from a background decode <c>Task</c> (later phases) to the main
/// thread's GPU copy pass. Knows nothing about textures or meshes specifically: each queued
/// action receives the copy-pass handle and performs whatever <c>SDL_UploadToGPUTexture</c>/
/// <c>SDL_UploadToGPUBuffer</c> call it needs, keeping this class usable by any future asset
/// kind without changes here.
/// </summary>
public sealed class PendingUploadQueue
{
    private readonly ConcurrentQueue<Action<IntPtr>> _pending = new();

    /// <summary>Queues <paramref name="upload"/> to run inside the next copy pass. Safe to call from any thread.</summary>
    public void Enqueue(Action<IntPtr> upload) => _pending.Enqueue(upload);

    /// <summary>Runs and removes every currently queued upload, passing it <paramref name="copyPass"/>. Call once per frame, inside a copy pass, from the render thread only.</summary>
    public void DrainInto(IntPtr copyPass)
    {
        while (_pending.TryDequeue(out var upload))
            upload(copyPass);
    }
}
